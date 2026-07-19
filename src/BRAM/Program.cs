using System;
using System.IO;
using System.Windows.Forms;
using RobloxAccountManager.UI;

namespace RobloxAccountManager;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Handle(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Handle(e.ExceptionObject as Exception);

        Application.Run(new MainForm());
    }

    private static void Handle(Exception? ex)
    {
        try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "bram-crash.txt"), ex?.ToString() ?? "unknown"); }
        catch { }
        MessageBox.Show("An unexpected error occurred:\n\n" + (ex?.Message ?? "unknown"),
            "BRAM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
