using System;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using RobloxAccountManager.Services;

namespace RobloxAccountManager;

public partial class MainWindow : Window
{
    private WebBridge? _bridge;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += (_, _) => _bridge?.Shutdown();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await WebViewHost.EnvironmentAsync();
            await Web.EnsureCoreWebView2Async(env);

            CoreWebView2 c = Web.CoreWebView2;

            string webRoot = Path.Combine(AppContext.BaseDirectory, "WebUI");
            c.SetVirtualHostNameToFolderMapping(
                "app.local", webRoot, CoreWebView2HostResourceAccessKind.Deny);

            c.Settings.IsStatusBarEnabled = false;
            c.Settings.AreBrowserAcceleratorKeysEnabled = false;
#if !DEBUG
            c.Settings.AreDevToolsEnabled = false;
            c.Settings.AreDefaultContextMenusEnabled = false;
#endif
            c.NewWindowRequested += (_, args) => args.Handled = true;

            _bridge = new WebBridge(c, this);
            _bridge.FitWindowRequested = FitHeightToContent;
            c.Navigate("https://app.local/index.html");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Couldn't start WebView2. Make sure the WebView2 Runtime is installed " +
                "(Evergreen runtime from Microsoft).\n\n" + ex.Message,
                "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    public void FitHeightToContent(int cssHeight)
    {
        if (cssHeight <= 0) return;

        double chrome = ActualHeight - Web.ActualHeight;
        if (double.IsNaN(chrome) || chrome < 0 || chrome > 250) chrome = 40;

        var area = SystemParameters.WorkArea;
        double target = cssHeight + chrome + 2;
        double newH = Math.Max(MinHeight, Math.Min(target, area.Height));
        if (Math.Abs(newH - Height) < 2) return;

        Height = newH;
        if (Top + newH > area.Bottom) Top = Math.Max(area.Top, area.Bottom - newH);
    }
}
