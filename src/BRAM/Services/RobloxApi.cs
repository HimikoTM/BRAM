using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RobloxAccountManager.Services;

public sealed class RobloxApi : IDisposable
{
    private const string AuthTicketUrl = "https://auth.roblox.com/v1/authentication-ticket";
    private readonly HttpClient _http;

    public RobloxApi()
    {
        var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false };
        _http = new HttpClient(handler);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("RobloxAccountManager/1.0");
    }

    private HttpRequestMessage Build(HttpMethod method, string url, string cookie, string? csrf = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.TryAddWithoutValidation("Cookie", ".ROBLOSECURITY=" + cookie);
        req.Headers.TryAddWithoutValidation("Referer", "https://www.roblox.com/");
        req.Headers.TryAddWithoutValidation("Origin", "https://www.roblox.com");
        if (csrf != null) req.Headers.TryAddWithoutValidation("x-csrf-token", csrf);
        return req;
    }

    public async Task<string?> GetCsrfTokenAsync(string cookie)
    {
        using var req = Build(HttpMethod.Post, AuthTicketUrl, cookie);
        using var res = await _http.SendAsync(req);
        if (res.Headers.TryGetValues("x-csrf-token", out var values))
            foreach (var v in values) return v;
        return null;
    }

    public async Task<string?> GetAuthTicketAsync(string cookie)
    {
        string? csrf = await GetCsrfTokenAsync(cookie);
        if (csrf is null) return null;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            using var req = Build(HttpMethod.Post, AuthTicketUrl, cookie, csrf);
            req.Content = new StringContent("", Encoding.UTF8, "application/json");
            using var res = await _http.SendAsync(req);

            if (res.Headers.TryGetValues("rbx-authentication-ticket", out var values))
                foreach (var v in values) return v;

            if (res.Headers.TryGetValues("x-csrf-token", out var fresh))
            {
                foreach (var v in fresh) { csrf = v; break; }
                continue;
            }
            break;
        }
        return null;
    }

    public async Task<(long id, string name, string display)?> GetAuthenticatedUserAsync(string cookie)
    {
        using var req = Build(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated", cookie);
        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;

        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var idEl))
                return null;
            long id = idEl.GetInt64();
            string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            string display = root.TryGetProperty("displayName", out var d) ? d.GetString() ?? "" : "";
            return (id, name, display);
        }
        catch
        {
            // 200 with a maintenance/interstitial body that isn't the expected JSON.
            return null;
        }
    }

    public async Task<int> GetPresenceAsync(long userId, string cookie)
    {
        using var req = Build(HttpMethod.Post, "https://presence.roblox.com/v1/presence/users", cookie);
        req.Content = new StringContent($"{{\"userIds\":[{userId}]}}", Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return -1;

        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("userPresences", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var p in arr.EnumerateArray())
                    if (p.TryGetProperty("userPresenceType", out var t) && t.TryGetInt32(out int v))
                        return v;
            return -1;
        }
        catch
        {
            return -1;
        }
    }

    public async Task<string?> GetAvatarHeadshotAsync(long userId)
    {
        string url = $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}" +
                     "&size=150x150&format=Png&isCircular=true";
        using var res = await _http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return null;

        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                foreach (var d in data.EnumerateArray())
                    if (d.TryGetProperty("imageUrl", out var u))
                        return u.GetString();
            return null;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
