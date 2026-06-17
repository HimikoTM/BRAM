using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RobloxAccountManager.Models;

namespace RobloxAccountManager.Services;

public enum VaultState
{
    NoFile,
    DpapiOnly,
    PasswordProtected,
    Corrupt
}

public sealed class Vault
{
    private const int Iterations = 310_000;
    private const int SaltLen = 16;
    private const int NonceLen = 12;
    private const int KeyLen = 32;
    private static readonly byte[] VerifierInfo = Encoding.UTF8.GetBytes("rbxam-verifier-v2");
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly object _sync = new();

    private byte[]? _key;
    private KdfInfo? _kdf;
    private VaultFile? _pending;

    public string Mode { get; private set; } = "none";
    public bool IsUnlocked { get; private set; }
    public List<Account> Accounts { get; private set; } = new();

    public VaultState Probe()
    {
        Reset();

        byte[]? primary = Storage.ReadAllBytesOrNull(Storage.AccountsPath);
        byte[]? backup = Storage.ReadAllBytesOrNull(Storage.BackupPath);
        if (primary == null && backup == null) { Mode = "none"; return VaultState.NoFile; }

        byte[]? plain = (primary != null ? TryDpapiUnprotect(primary) : null)
                     ?? (backup != null ? TryDpapiUnprotect(backup) : null);
        if (plain == null)
        {
            Mode = "none";
            return VaultState.Corrupt;
        }

        string text = Encoding.UTF8.GetString(plain).TrimStart('﻿', ' ', '\t', '\r', '\n');

        if (text.StartsWith("["))
        {
            Mode = "dpapi";
            Accounts = DeserializeAccounts(text);
            IsUnlocked = true;
            return VaultState.DpapiOnly;
        }

        VaultFile? vf;
        try { vf = JsonSerializer.Deserialize<VaultFile>(text); }
        catch { vf = null; }
        if (vf == null) { Mode = "none"; return VaultState.Corrupt; }

        if (string.Equals(vf.Mode, "password", StringComparison.OrdinalIgnoreCase))
        {
            Mode = "password";
            _pending = vf;
            return VaultState.PasswordProtected;
        }

        try
        {
            Accounts = DeserializeAccounts(Encoding.UTF8.GetString(Convert.FromBase64String(vf.Cipher)));
        }
        catch { Mode = "none"; return VaultState.Corrupt; }
        Mode = "dpapi";
        IsUnlocked = true;
        return VaultState.DpapiOnly;
    }

    public bool Unlock(string password)
    {
      lock (_sync)
      {
        if (_pending?.Kdf == null || _pending.Verifier == null || _pending.Nonce == null || _pending.Tag == null)
            return false;

        VaultFile vf = _pending;
        byte[] salt = Convert.FromBase64String(vf.Kdf!.Salt);
        byte[] pwBytes = Encoding.UTF8.GetBytes(password);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(pwBytes, salt, vf.Kdf.Iterations, HashAlgorithmName.SHA256, KeyLen);
        CryptographicOperations.ZeroMemory(pwBytes);

        byte[] expected = Convert.FromBase64String(vf.Verifier!);
        byte[] actual = DeriveVerifier(key);
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            CryptographicOperations.ZeroMemory(key);
            return false;
        }

        try
        {
            byte[] nonce = Convert.FromBase64String(vf.Nonce!);
            byte[] tag = Convert.FromBase64String(vf.Tag!);
            byte[] cipher = Convert.FromBase64String(vf.Cipher);
            byte[] plain = new byte[cipher.Length];
            using (var gcm = new AesGcm(key, tag.Length))
                gcm.Decrypt(nonce, cipher, tag, plain);

            Accounts = DeserializeAccounts(Encoding.UTF8.GetString(plain));
            CryptographicOperations.ZeroMemory(plain);
            _key = key;
            _kdf = vf.Kdf;
            Mode = "password";
            IsUnlocked = true;
            _pending = null;
            return true;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            return false;
        }
      }
    }

    public void CreateNew(string? password)
    {
        lock (_sync)
        {
            Reset();
            Accounts = new();
            ApplyMode(password);
            IsUnlocked = true;
            Save();
        }
    }

    public void ChangeMasterPassword(string? password)
    {
        lock (_sync)
        {
            if (!IsUnlocked) throw new InvalidOperationException("Vault is locked.");
            ApplyMode(password);
            Save();
        }
    }

    public void Save()
    {
      lock (_sync)
      {
        if (!IsUnlocked) return;

        byte[] accBytes = JsonSerializer.SerializeToUtf8Bytes(Accounts, JsonOpts);
        var vf = new VaultFile { Version = 2, Mode = Mode == "password" ? "password" : "dpapi" };

        if (Mode == "password")
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceLen);
            byte[] cipher = new byte[accBytes.Length];
            byte[] tag = new byte[16];
            using (var gcm = new AesGcm(_key!, tag.Length))
                gcm.Encrypt(nonce, accBytes, cipher, tag);

            vf.Kdf = _kdf;
            vf.Verifier = Convert.ToBase64String(DeriveVerifier(_key!));
            vf.Nonce = Convert.ToBase64String(nonce);
            vf.Tag = Convert.ToBase64String(tag);
            vf.Cipher = Convert.ToBase64String(cipher);
        }
        else
        {
            vf.Cipher = Convert.ToBase64String(accBytes);
        }

        CryptographicOperations.ZeroMemory(accBytes);

        byte[] container = JsonSerializer.SerializeToUtf8Bytes(vf, JsonOpts);
        byte[] wrapped = ProtectedData.Protect(container, null, DataProtectionScope.CurrentUser);
        Storage.WriteAllBytesAtomic(Storage.AccountsPath, wrapped, Storage.BackupPath);
      }
    }

    public void Lock()
    {
        lock (_sync)
        {
            if (_key != null) CryptographicOperations.ZeroMemory(_key);
            _key = null;
            _kdf = null;
            Accounts = new();
            IsUnlocked = false;
            Probe();
        }
    }

    private void Reset()
    {
        if (_key != null) CryptographicOperations.ZeroMemory(_key);
        _key = null;
        _kdf = null;
        _pending = null;
        Accounts = new();
        IsUnlocked = false;
        Mode = "none";
    }

    private void ApplyMode(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            if (_key != null) CryptographicOperations.ZeroMemory(_key);
            _key = null;
            _kdf = null;
            Mode = "dpapi";
        }
        else
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltLen);
            byte[] pwBytes = Encoding.UTF8.GetBytes(password);
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(pwBytes, salt, Iterations, HashAlgorithmName.SHA256, KeyLen);
            CryptographicOperations.ZeroMemory(pwBytes);
            if (_key != null) CryptographicOperations.ZeroMemory(_key);
            _key = key;
            _kdf = new KdfInfo { Algo = "PBKDF2-SHA256", Iterations = Iterations, Salt = Convert.ToBase64String(salt) };
            Mode = "password";
        }
    }

    private static byte[] DeriveVerifier(byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(VerifierInfo);
    }

    private static List<Account> DeserializeAccounts(string json)
    {
        try { return JsonSerializer.Deserialize<List<Account>>(json, JsonOpts) ?? new(); }
        catch { return new(); }
    }

    private static byte[]? TryDpapiUnprotect(byte[] data)
    {
        try { return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser); }
        catch { return null; }
    }
}
