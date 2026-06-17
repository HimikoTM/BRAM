using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace RobloxAccountManager.Services;

public static class WebViewHost
{
    private static Task<CoreWebView2Environment>? _env;

    public static Task<CoreWebView2Environment> EnvironmentAsync()
        => _env ??= CoreWebView2Environment.CreateAsync(null, Storage.WebViewProfileDir);
}
