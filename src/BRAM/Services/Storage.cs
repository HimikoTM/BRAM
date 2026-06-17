using System;
using System.IO;

namespace RobloxAccountManager.Services;

public static class Storage
{
    public static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobloxAccountManager");

    public static string AccountsPath => Path.Combine(Dir, "accounts.dat");

    public static string BackupPath => AccountsPath + ".bak";

    public static string SettingsPath => Path.Combine(Dir, "settings.json");

    public static string ActivityLogPath => Path.Combine(Dir, "activity.log.json");

    public static string WebViewProfileDir => Path.Combine(Dir, "WebView2");

    public static void EnsureDir() => Directory.CreateDirectory(Dir);

    public static byte[]? ReadAllBytesOrNull(string path)
    {
        try { return File.Exists(path) ? File.ReadAllBytes(path) : null; }
        catch { return null; }
    }

    public static void WriteAllBytes(string path, byte[] data)
    {
        EnsureDir();
        File.WriteAllBytes(path, data);
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
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch { return null; }
    }

    public static void WriteAllText(string path, string text)
    {
        EnsureDir();
        File.WriteAllText(path, text);
    }
}
