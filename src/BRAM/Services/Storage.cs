using System;
using System.IO;
using System.Text;
using System.Threading;

namespace RobloxAccountManager.Services;

public static class Storage
{
    public static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobloxAccountManager");

    public static string AccountsPath => Path.Combine(Dir, "accounts.dat");

    public static string BackupPath => AccountsPath + ".bak";

    public static string SettingsPath => Path.Combine(Dir, "settings.json");

    public static string ActivityLogPath => Path.Combine(Dir, "activity.log.json");

    public static void EnsureDir() => Directory.CreateDirectory(Dir);

    public static bool Exists(string path)
    {
        try { return File.Exists(path); }
        catch { return false; }
    }

    /// <summary>
    /// Reads a file, distinguishing "absent" (returns null) from "present but
    /// momentarily locked". A transient lock (AV scan, backup client, indexer)
    /// is retried a few times so an intact vault is never mistaken for missing.
    /// Callers must additionally check <see cref="Exists"/> before treating a
    /// null as "no data" — see Vault.Probe.
    /// </summary>
    public static byte[]? ReadAllBytesOrNull(string path)
    {
        if (!Exists(path)) return null;
        for (int attempt = 0; ; attempt++)
        {
            try { return File.ReadAllBytes(path); }
            catch (IOException) when (attempt < 4) { Thread.Sleep(120); }
            catch (UnauthorizedAccessException) when (attempt < 4) { Thread.Sleep(120); }
            catch { return null; }
        }
    }

    public static void WriteAllBytesAtomic(string path, byte[] data, string? backupPath = null)
    {
        EnsureDir();
        string tmp = path + ".tmp";
        File.WriteAllBytes(tmp, data);
        if (File.Exists(path))
            File.Replace(tmp, path, backupPath, ignoreMetadataErrors: true);
        else
            File.Move(tmp, path);
    }

    public static string? ReadAllTextOrNull(string path)
    {
        try { return Exists(path) ? File.ReadAllText(path) : null; }
        catch { return null; }
    }

    /// <summary>Atomic text write (temp file + replace) so a crash mid-write
    /// never truncates the previous good file.</summary>
    public static void WriteAllTextAtomic(string path, string text)
    {
        EnsureDir();
        WriteAllBytesAtomic(path, Encoding.UTF8.GetBytes(text));
    }
}
