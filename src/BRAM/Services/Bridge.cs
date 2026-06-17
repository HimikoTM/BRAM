using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using RobloxAccountManager.Models;

namespace RobloxAccountManager.Services;

public sealed class WebBridge
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly CoreWebView2 _web;
    private readonly Dispatcher _dispatcher;

    public Action<int>? FitWindowRequested;

    private readonly RobloxApi _api = new();
    private readonly Vault _vault = new();
    private readonly Settings _settings = Settings.Load();
    private readonly ActivityLog _log = new();
    private readonly PresenceRefresher _refresher;
    private readonly SemaphoreSlim _sweepGate = new(1, 1);

    public WebBridge(CoreWebView2 web, Window owner)
    {
        _web = web;
        _dispatcher = owner.Dispatcher;
        _refresher = new PresenceRefresher(RefreshAllAsync, () => _settings.AutoRefreshSeconds);
        _web.WebMessageReceived += OnMessage;
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string id = "";
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            JsonElement root = doc.RootElement;
            id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            string method = root.TryGetProperty("method", out var mEl) ? mEl.GetString() ?? "" : "";
            JsonElement args = root.TryGetProperty("args", out var aEl) ? aEl : default;

            object? result = await Dispatch(method, args);
            Reply(id, true, result, null);
        }
        catch (Exception ex)
        {
            Reply(id, false, null, ex.Message);
        }
    }

    private async Task<object?> Dispatch(string method, JsonElement args) => method switch
    {
        "init"             => Init(),
        "unlock"           => Unlock(Str(args, "password")),
        "createVault"      => CreateVault(StrOrNull(args, "password")),
        "resetVault"       => ResetVault(StrOrNull(args, "password")),
        "setMasterPassword"=> SetMasterPassword(StrOrNull(args, "password")),
        "lock"             => Lock(),
        "getState"         => FullState(),
        "addByCookie"      => await AddByCookieAsync(Str(args, "cookie")),
        "beginLogin"       => await BeginLoginAsync(),
        "launch"           => await LaunchAsync(Str(args, "id"), LongOrNull(args, "placeId")),
        "openClient"       => OpenClient(),
        "refreshOne"       => await RefreshOneAsync(Str(args, "id")),
        "refreshAll"       => await RefreshAllPublicAsync(),
        "editAccount"      => EditAccount(args),
        "deleteAccount"    => DeleteAccount(Str(args, "id")),
        "copyCookie"       => CopyCookie(Str(args, "id")),
        "setMultiInstance" => SetMultiInstance(Bool(args, "enabled")),
        "setPlaceId"       => SetPlaceId(Str(args, "placeId")),
        "setAutoRefresh"   => SetAutoRefresh(Int(args, "seconds")),
        "fitWindow"        => FitWindow(Int(args, "height")),
        "clearLog"         => ClearLog(),
        _ => throw new InvalidOperationException($"Unknown method: {method}")
    };

    private object Init()
    {
        VaultState state = _vault.Probe();
        return state switch
        {
            VaultState.PasswordProtected => new { mode = "password", locked = true, needsSetup = false, corrupt = false },
            VaultState.NoFile            => new { mode = "none", locked = false, needsSetup = true, corrupt = false },
            VaultState.Corrupt           => new { mode = "corrupt", locked = false, needsSetup = false, corrupt = true },
            _                            => OnUnlocked(new { mode = "dpapi", locked = false, needsSetup = false, corrupt = false, state = FullState() })
        };
    }

    private object Unlock(string password)
    {
        if (!_vault.Unlock(password)) return new { ok = false };
        Add("BOOT", $"Vault decrypted — {_vault.Accounts.Count} session(s) restored.", "#2dff8a");
        return OnUnlocked(new { ok = true, state = FullState() });
    }

    private object CreateVault(string? password)
    {
        _vault.CreateNew(password);
        Add("BOOT", password == null ? "Vault created (DPAPI-only)." : "Vault created with master password.", "#2dff8a");
        return OnUnlocked(new { ok = true, mode = _vault.Mode, state = FullState() });
    }

    private object ResetVault(string? password)
    {
        _vault.CreateNew(password);
        Add("VAULT", "Vault reset — previous unreadable data was discarded.", "#ffc24d");
        return OnUnlocked(new { ok = true, mode = _vault.Mode, state = FullState() });
    }

    private object SetMasterPassword(string? password)
    {
        _vault.ChangeMasterPassword(password);
        Add("VAULT", password == null ? "Master password removed (DPAPI-only)." : "Master password updated.", "#5d7a6c");
        return new { ok = true, mode = _vault.Mode };
    }

    private object? Lock()
    {
        _refresher.Stop();
        _vault.Lock();
        Emit("vaultLocked", new { });
        return null;
    }

    private T OnUnlocked<T>(T payload)
    {
        if (_settings.MultiInstance) Launcher.EnableMultiInstance();
        _refresher.Start();
        return payload;
    }

    private async Task<object?> AddByCookieAsync(string cookie)
    {
        cookie = cookie.Trim();
        var user = await _api.GetAuthenticatedUserAsync(cookie);
        if (user is null)
            throw new InvalidOperationException("That cookie isn't valid — couldn't load the account.");
        if (_vault.Accounts.Any(a => a.UserId == user.Value.id))
            throw new InvalidOperationException($"{user.Value.name} is already in the vault.");

        var acc = new Account
        {
            SecurityCookie = cookie,
            UserId = user.Value.id,
            Username = user.Value.name,
            Label = user.Value.display
        };
        _vault.Accounts.Add(acc);
        _vault.Save();
        Add("ADD", $"Added {acc.Username}.", "#2dff8a");
        EmitAccount(acc);
        await RefreshAccountAsync(acc, CancellationToken.None);
        return new { id = acc.Id };
    }

    private async Task<object?> BeginLoginAsync()
    {
        string? cookie;
        try
        {
            cookie = await BrowserLogin.CaptureCookieAsync();
        }
        catch (Exception ex)
        {
            Add("WARN", "Browser login failed: " + ex.Message, "#ffc24d");
            throw new InvalidOperationException("Browser login failed: " + ex.Message);
        }
        if (string.IsNullOrEmpty(cookie)) return new { cancelled = true };
        return await AddByCookieAsync(cookie);
    }

    private async Task<object?> LaunchAsync(string id, long? placeIdArg)
    {
        Account? a = Find(id);
        if (a is null || a.IsBusy) return null;

        a.IsBusy = true;
        a.Status = "Authenticating…";
        EmitAccount(a);
        try
        {
            if (_settings.MultiInstance && !Launcher.MultiInstanceEnabled) TryEnableMulti();

            string? ticket = await _api.GetAuthTicketAsync(a.SecurityCookie);
            if (ticket is null)
            {
                a.Status = "Login expired — re-add";
                Add("WARN", $"{a.Username}: saved login expired.", "#ffc24d");
                throw new InvalidOperationException(
                    $"Couldn't get an auth ticket for {a.Username}. The saved login is likely expired — remove and re-add it.");
            }

            long placeId = placeIdArg ?? _settings.DefaultPlaceId;
            Launcher.LaunchAuthenticated(ticket, placeId);

            a.LastUsed = DateTime.Now;
            a.Status = "Launched";
            _vault.Save();
            Add("LAUNCH", placeId > 0 ? $"Launched {a.Username} → place {placeId}." : $"Launched {a.Username}.", "#2dff8a");
            return new { ok = true };
        }
        finally
        {
            a.IsBusy = false;
            EmitAccount(a);
        }
    }

    private object? OpenClient()
    {
        if (_settings.MultiInstance && !Launcher.MultiInstanceEnabled) TryEnableMulti();
        Launcher.LaunchManual();
        Add("INFO", "Opened a clean Roblox client.", "#5d7a6c");
        return null;
    }

    private object? EditAccount(JsonElement args)
    {
        Account? a = Find(Str(args, "id"));
        if (a is null) return null;
        if (args.TryGetProperty("label", out var l)) a.Label = l.GetString() ?? "";
        if (args.TryGetProperty("group", out var g))
        {
            string grp = (g.GetString() ?? "").Trim();
            a.Group = string.IsNullOrWhiteSpace(grp) ? "General" : grp;
        }
        if (args.TryGetProperty("notes", out var n)) a.Notes = n.GetString() ?? "";
        _vault.Save();
        Add("EDIT", $"Updated {a.Username}.", "#5d7a6c");
        EmitAccount(a);
        return null;
    }

    private object? DeleteAccount(string id)
    {
        Account? a = Find(id);
        if (a is null) return null;
        _vault.Accounts.Remove(a);
        _vault.Save();
        Add("WARN", $"Removed {a.Username} from the vault.", "#ffc24d");
        Emit("accountRemoved", new { id });
        return null;
    }

    private object? CopyCookie(string id)
    {
        Account? a = Find(id);
        if (a is null) return null;
        Clipboard.SetText(a.SecurityCookie);
        Add("INFO", $".ROBLOSECURITY for {a.Username} copied to clipboard.", "#5d7a6c");
        return null;
    }

    private async Task<object?> RefreshOneAsync(string id)
    {
        Account? a = Find(id);
        if (a != null) await RefreshAccountAsync(a, CancellationToken.None);
        return null;
    }

    private async Task<object?> RefreshAllPublicAsync()
    {
        await RefreshAllAsync(CancellationToken.None, manual: true);
        return null;
    }

    private Task RefreshAllAsync(CancellationToken ct) => RefreshAllAsync(ct, manual: false);

    private async Task RefreshAllAsync(CancellationToken ct, bool manual)
    {
        if (!_vault.IsUnlocked) return;
        if (!await _sweepGate.WaitAsync(0, ct))
        {
            if (manual) Emit("toast", new { text = "A refresh is already in progress…" });
            return;
        }
        try
        {
            List<Account> list = _vault.Accounts.ToList();
            int total = list.Count, done = 0;
            Emit("refreshProgress", new { done, total });
            foreach (Account a in list)
            {
                ct.ThrowIfCancellationRequested();
                await RefreshAccountAsync(a, ct, save: false);
                Emit("refreshProgress", new { done = ++done, total });
            }
            _vault.Save();
        }
        finally
        {
            _sweepGate.Release();
        }
    }

    private async Task RefreshAccountAsync(Account a, CancellationToken ct, bool save = true)
    {
        if (a.IsBusy) return;
        a.IsBusy = true;
        a.Status = "Checking…";
        EmitAccount(a);
        try
        {
            var user = await _api.GetAuthenticatedUserAsync(a.SecurityCookie);
            if (user is null)
            {
                a.Status = "Logged out — re-add";
                return;
            }

            a.UserId = user.Value.id;
            a.Username = user.Value.name;
            if (string.IsNullOrEmpty(a.Label)) a.Label = user.Value.display;

            string? avatar = await _api.GetAvatarHeadshotAsync(a.UserId);
            if (!string.IsNullOrEmpty(avatar)) a.AvatarUrl = avatar!;

            int presence = await _api.GetPresenceAsync(a.UserId, a.SecurityCookie);
            a.Status = presence switch
            {
                0 => "Offline",
                1 => "Online",
                2 => "In game",
                3 => "In Studio",
                _ => "Unknown"
            };
            if (save) _vault.Save();
        }
        catch
        {
            a.Status = "Error checking";
        }
        finally
        {
            a.IsBusy = false;
            EmitAccount(a);
        }
    }

    private object? SetMultiInstance(bool enabled)
    {
        _settings.MultiInstance = enabled;
        _settings.Save();
        if (enabled) TryEnableMulti();
        else { Launcher.DisableMultiInstance(); Add("INFO", "Multi-instance OFF.", "#5d7a6c"); }
        return new { active = Launcher.MultiInstanceEnabled };
    }

    private object? SetPlaceId(string placeId)
    {
        _settings.DefaultPlaceId = long.TryParse(placeId?.Trim(), out long p) ? p : 0;
        _settings.Save();
        return null;
    }

    private object? SetAutoRefresh(int seconds)
    {
        _settings.AutoRefreshSeconds = Math.Max(0, seconds);
        _settings.Save();
        _refresher.Start();
        return new { seconds = _settings.AutoRefreshSeconds };
    }

    private object? ClearLog()
    {
        _log.Clear();
        return null;
    }

    private object? FitWindow(int contentHeight)
    {
        FitWindowRequested?.Invoke(contentHeight);
        return null;
    }

    private void TryEnableMulti()
    {
        if (Launcher.EnableMultiInstance())
        {
            Add("MULTI", "Multi-instance ON — holding ROBLOX_singletonEvent.", "#ffc24d");
            return;
        }
        Add("WARN", "Multi-instance: Roblox is already running — close all Roblox windows first.", "#ffc24d");
        Emit("toast", new { text = "Close every Roblox window, then re-enable Multi-instance." });
    }

    private Account? Find(string id) => _vault.Accounts.FirstOrDefault(x => x.Id == id);

    private object FullState() => new
    {
        accounts = _vault.IsUnlocked ? _vault.Accounts.Select(ToDto).ToArray() : Array.Empty<object>(),
        settings = new
        {
            multiInstance = _settings.MultiInstance,
            placeId = _settings.DefaultPlaceId,
            autoRefreshSeconds = _settings.AutoRefreshSeconds
        },
        vaultMode = _vault.Mode,
        multiInstanceActive = Launcher.MultiInstanceEnabled,
        runningClients = Launcher.RunningClientCount(),
        log = _log.Tail()
    };

    private static object ToDto(Account a) => new
    {
        id = a.Id,
        label = a.Label,
        username = a.Username,
        userId = a.UserId.ToString(),
        group = a.Group,
        notes = a.Notes,
        status = a.Status,
        avatarUrl = a.AvatarUrl,
        lastUsed = a.LastUsed?.ToString("o"),
        dateAdded = a.DateAdded.ToString("o")
    };

    private void Add(string tag, string text, string color)
    {
        LogEntry entry = _log.Add(tag, text, color);
        Emit("log", entry);
    }

    private void EmitAccount(Account a) => Emit("accountUpdated", ToDto(a));

    private void Emit(string @event, object data) =>
        PostToJs(JsonSerializer.Serialize(new { kind = "evt", @event, data }, JsonOpts));

    private void Reply(string id, bool ok, object? result, string? error) =>
        PostToJs(JsonSerializer.Serialize(new { kind = "res", id, ok, result, error }, JsonOpts));

    private void PostToJs(string json)
    {
        if (_dispatcher.CheckAccess()) _web.PostWebMessageAsJson(json);
        else _dispatcher.InvokeAsync(() => _web.PostWebMessageAsJson(json));
    }

    private static string Str(JsonElement a, string name) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static string? StrOrNull(JsonElement a, string name) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static bool Bool(JsonElement a, string name)
    {
        if (a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
            if (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b)) return b;
        }
        return false;
    }

    private static int Int(JsonElement a, string name)
    {
        if (a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int n)) return n;
            if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out int s)) return s;
        }
        return 0;
    }

    private static long? LongOrNull(JsonElement a, string name)
    {
        if (a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n)) return n;
            if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out long s)) return s;
        }
        return null;
    }

    public void Shutdown()
    {
        _refresher.Stop();
        _settings.Save();
        Launcher.DisableMultiInstance();
        _api.Dispose();
    }
}
