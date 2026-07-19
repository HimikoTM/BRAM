using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RobloxAccountManager.UI;

/// <summary>Soft light palette + shared fonts and drawing helpers.</summary>
internal static class Theme
{
    private static Color Hex(string h) => ColorTranslator.FromHtml(h);

    // Surfaces — soft, not harsh white
    public static readonly Color Bg = Hex("#F3F4F6");
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceHover = Hex("#EEF0F3");
    public static readonly Color Border = Hex("#E5E7EB");
    public static readonly Color BorderStrong = Hex("#D5D9DF");

    // Text
    public static readonly Color Text = Hex("#1F2937");
    public static readonly Color Text2 = Hex("#6B7280");
    public static readonly Color Muted = Hex("#9CA3AF");

    // Accent + semantic
    public static readonly Color Accent = Hex("#2563EB");
    public static readonly Color AccentHover = Hex("#1D4ED8");
    public static readonly Color AccentDown = Hex("#1E40AF");
    public static readonly Color Green = Hex("#16A34A");
    public static readonly Color Amber = Hex("#D97706");
    public static readonly Color Red = Hex("#DC2626");
    public static readonly Color Gray = Hex("#9CA3AF");

    public static readonly Color ChipBg = Hex("#EEF0F3");

    // Fonts
    public static readonly Font Font = new("Segoe UI", 9.75f);
    public static readonly Font FontSemibold = new("Segoe UI Semibold", 9.75f);
    public static readonly Font FontSmall = new("Segoe UI", 8.5f);
    public static readonly Font FontTitle = new("Segoe UI Semibold", 15f);
    public static readonly Font FontStat = new("Segoe UI", 21f, FontStyle.Bold);
    public static readonly Font FontMono = new("Consolas", 9f);
    public static readonly Font FontIcon = new("Segoe MDL2 Assets", 10f);

    public static Color StatusColor(string? status) => status switch
    {
        "In game" => Accent,
        "Online" => Green,
        "Launched" => Accent,
        "In Studio" => Amber,
        "Offline" => Gray,
        "Logged out — re-add" or "Login expired — re-add" or "Error checking" => Red,
        _ => Gray,
    };

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        if (radius <= 0) { p.AddRectangle(r); p.CloseFigure(); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ---- Button factories (self-painted UiButton, consistent at any DPI) ----

    public static UiButton Primary(string text) => new(UiButton.Kind.Primary) { Text = text };
    public static UiButton Secondary(string text) => new(UiButton.Kind.Secondary) { Text = text };
    public static UiButton Subtle(string text) => new(UiButton.Kind.Subtle) { Text = text };
    public static UiButton Danger(string text) => new(UiButton.Kind.Danger) { Text = text };

    public static UiButton IconBtn(string glyph, Color? fore = null)
    {
        var b = new UiButton(UiButton.Kind.Icon) { Text = glyph, Width = 32, Height = 32 };
        if (fore != null) b.ForeColor = fore.Value;
        return b;
    }
}
