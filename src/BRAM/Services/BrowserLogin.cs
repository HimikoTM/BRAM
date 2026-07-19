using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace RobloxAccountManager.Services;

public static class BrowserLogin
{
    private const string LoginUrl = "https://www.roblox.com/login";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);

    public static async Task<string?> CaptureCookieAsync(CancellationToken ct = default)
    {
        string exe = FindBrowser()
            ?? throw new InvalidOperationException(
                "No Chrome or Edge was found for browser login. Install Google Chrome, or add the account via “Add (cookie)”.");

        string profile = Path.Combine(Path.GetTempPath(), "RAM-login-" + Guid.NewGuid().ToString("N"));

        var options = new LaunchOptions
        {
            Headless = false,
            ExecutablePath = exe,
            UserDataDir = profile,
            DefaultViewport = null,
            Args = new[]
            {
                "--no-first-run", "--no-default-browser-check",
                "--window-size=980,760", "--window-position=160,80"
            }
        };

        IBrowser? browser = null;
        bool closedByUser = false;
        try
        {
            browser = await Puppeteer.LaunchAsync(options);
            browser.Disconnected += (_, _) => closedByUser = true;

            var pages = await browser.PagesAsync();
            IPage page = pages.Length > 0 ? pages[0] : await browser.NewPageAsync();
            try { await page.GoToAsync(LoginUrl); } catch { }

            DateTime start = DateTime.UtcNow;
            while (!closedByUser && !ct.IsCancellationRequested && DateTime.UtcNow - start < Timeout)
            {
                try
                {
                    var cookies = await page.GetCookiesAsync("https://www.roblox.com", "https://roblox.com");
                    var sec = cookies.FirstOrDefault(c => c.Name == ".ROBLOSECURITY");
                    string url = page.Url ?? "";
                    if (sec != null && !string.IsNullOrEmpty(sec.Value)
                        && !url.Contains("/login", StringComparison.OrdinalIgnoreCase))
                    {
                        return sec.Value;
                    }
                }
                catch { }

                await Task.Delay(700, ct);
            }
            return null;
        }
        finally
        {
            try { if (browser != null) await browser.CloseAsync(); } catch { }
            await SafeDeleteDirAsync(profile);
        }
    }

    /// <summary>
    /// Deletes a Chromium profile dir, retrying because Chrome's child processes
    /// often still hold handles for a moment after CloseAsync returns on Windows.
    /// </summary>
    private static async Task SafeDeleteDirAsync(string dir)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                Directory.Delete(dir, recursive: true);
                return;
            }
            catch when (attempt < 5)
            {
                await Task.Delay(300);
            }
            catch
            {
                return; // give up quietly; PruneStaleProfiles() will collect it later
            }
        }
    }

    /// <summary>Best-effort cleanup of profiles leaked by earlier sessions.</summary>
    public static void PruneStaleProfiles()
    {
        try
        {
            foreach (string dir in Directory.EnumerateDirectories(Path.GetTempPath(), "RAM-login-*"))
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }
        catch { }
    }

    private static string? FindBrowser()
    {
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string[] candidates =
        {
            Path.Combine(pf,    "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(pfx86, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(lad,   "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(pfx86, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(pf,    "Microsoft", "Edge", "Application", "msedge.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
