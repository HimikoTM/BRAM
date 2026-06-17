using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace RobloxAccountManager.Services;

public static class Launcher
{
    private static Mutex? _singleton;

    public static bool MultiInstanceEnabled => _singleton != null;

    public static bool EnableMultiInstance()
    {
        if (_singleton != null) return true;
        try
        {
            _singleton = new Mutex(true, "ROBLOX_singletonEvent", out bool createdNew);
            if (!createdNew)
            {
                _singleton.Dispose();
                _singleton = null;
                return false;
            }
            return true;
        }
        catch
        {
            _singleton = null;
            return false;
        }
    }

    public static void DisableMultiInstance()
    {
        try { _singleton?.ReleaseMutex(); } catch { }
        _singleton?.Dispose();
        _singleton = null;
    }

    public static string? FindRobloxPlayer()
    {
        string versions = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Roblox", "Versions");
        if (!Directory.Exists(versions)) return null;

        return Directory.GetDirectories(versions)
            .Select(d => Path.Combine(d, "RobloxPlayerBeta.exe"))
            .Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static void LaunchAuthenticated(string ticket, long placeId)
    {
        long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string launchUri;

        if (placeId > 0)
        {
            string placeLauncher =
                "https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame" +
                $"&browserTrackerId=0&placeId={placeId}&isPlayTogetherGame=false";

            launchUri =
                "roblox-player:1+launchmode:play" +
                "+gameinfo:" + ticket +
                "+launchtime:" + ms +
                "+placelauncherurl:" + Uri.EscapeDataString(placeLauncher) +
                "+browsertrackerid:0+robloxLocale:en_us+gameLocale:en_us+channel:";
        }
        else
        {
            launchUri =
                "roblox-player:1+launchmode:app" +
                "+gameinfo:" + ticket +
                "+launchtime:" + ms +
                "+browsertrackerid:0+robloxLocale:en_us+gameLocale:en_us+channel:";
        }

        Process.Start(new ProcessStartInfo { FileName = launchUri, UseShellExecute = true });
    }

    public static void LaunchManual()
    {
        string? exe = FindRobloxPlayer();
        if (exe != null)
            Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
        else
            Process.Start(new ProcessStartInfo { FileName = "roblox-player:1+launchmode:app", UseShellExecute = true });
    }

    public static int RunningClientCount()
    {
        try { return Process.GetProcessesByName("RobloxPlayerBeta").Length; }
        catch { return 0; }
    }
}
