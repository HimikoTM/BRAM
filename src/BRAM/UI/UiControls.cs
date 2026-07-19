using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RobloxAccountManager.UI;

/// <summary>Rounded surface panel with a thin border; corners reveal the parent bg.</summary>
internal class Card : Panel
{
    public int Radius { get; set; } = 8;
    public Color Fill { get; set; } = Theme.Surface;
    public Color BorderColor { get; set; } = Theme.Border;

    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
    }

    protected override void OnPaintBackground(PaintEventArgs e) { /* painted in OnPaint */ }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.Round(r, Radius);
        using var fill = new SolidBrush(Fill);
        g.FillPath(fill, path);
        using var pen = new Pen(BorderColor, 1);
        g.DrawPath(pen, path);
    }
}

/// <summary>Circular avatar with a status dot; falls back to a coloured initial.</summary>
internal class AvatarControl : Control
{
    private Image? _img;
    public Color StatusColor { get; set; } = Theme.Gray;
    public Color RingColor { get; set; } = Theme.Surface;
    public string Initial { get; set; } = "?";
    public Color InitialBg { get; set; } = Theme.Accent;

    public AvatarControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Size = new Size(46, 46);
    }

    public Image? Image
    {
        get => _img;
        set { _img = value; Invalidate(); }
    }

    public void Set(string initial, Color initialBg, Color status)
    {
        Initial = initial;
        InitialBg = initialBg;
        StatusColor = status;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        int d = Math.Min(Width, Height) - 1;
        var circle = new Rectangle(0, 0, d, d);

        using (var clip = new GraphicsPath())
        {
            clip.AddEllipse(circle);
            var save = g.Save();
            g.SetClip(clip);
            if (_img != null)
            {
                g.DrawImage(_img, circle);
            }
            else
            {
                using var b = new SolidBrush(InitialBg);
                g.FillEllipse(b, circle);
                TextRenderer.DrawText(g, Initial, new Font("Segoe UI", d * 0.42f, FontStyle.Bold),
                    circle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            g.Restore(save);
        }

        // status dot (bottom-right) with a ring matching the card fill
        int ds = Math.Max(11, d / 4);
        var dot = new Rectangle(Width - ds - 1, Height - ds - 1, ds, ds);
        using (var ring = new SolidBrush(RingColor))
            g.FillEllipse(ring, Rectangle.Inflate(dot, 2, 2));
        using (var sb = new SolidBrush(StatusColor))
            g.FillEllipse(sb, dot);
    }
}

/// <summary>Downloads and caches avatar images by URL (call on the UI thread).</summary>
internal static class AvatarCache
{
    private static readonly HttpClient Http = new();
    private static readonly Dictionary<string, Image> Cache = new();

    public static async Task<Image?> GetAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (Cache.TryGetValue(url, out var cached)) return cached;
        try
        {
            byte[] bytes = await Http.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            using var raw = Image.FromStream(ms);
            var copy = new Bitmap(raw);
            Cache[url] = copy;
            return copy;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Small rounded pill label used for the group chip.</summary>
internal class Chip : Control
{
    private string _text = "";
    public Chip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.FontSmall;
        Height = 20;
    }

    public new string Text
    {
        get => _text;
        set { _text = value; Width = TextRenderer.MeasureText(value, Font).Width + 18; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.Round(r, 5);
        using var b = new SolidBrush(Theme.ChipBg);
        g.FillPath(b, path);
        TextRenderer.DrawText(g, _text, Font, r, Theme.Text2,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>A 34px-tall text field (matches button height) with a centered borderless textbox.</summary>
internal class FieldBox : Panel
{
    public TextBox Inner { get; }
    private bool _focused;

    public FieldBox(bool mono = false)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 34;
        BackColor = Color.White;
        Cursor = Cursors.IBeam;
        Inner = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = mono ? Theme.FontMono : Theme.Font,
            BackColor = Color.White,
            ForeColor = Theme.Text
        };
        Inner.GotFocus += (_, _) => { _focused = true; Invalidate(); };
        Inner.LostFocus += (_, _) => { _focused = false; Invalidate(); };
        Controls.Add(Inner);
        LayoutInner();
    }

    public string PlaceholderText { get => Inner.PlaceholderText; set => Inner.PlaceholderText = value; }

    [AllowNull]
    public override string Text { get => Inner.Text; set => Inner.Text = value ?? string.Empty; }

    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); LayoutInner(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Inner.Focus(); }

    private void LayoutInner()
    {
        if (Inner is null) return; // fires during base ctor sizing, before Inner is created
        int ph = Inner.PreferredHeight;
        Inner.SetBounds(9, Math.Max(1, (ClientSize.Height - ph) / 2), Math.Max(10, ClientSize.Width - 18), ph);
    }

    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.White);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using var pen = new Pen(_focused ? Theme.Accent : Theme.BorderStrong, 1);
        g.DrawRectangle(pen, r);
    }
}

/// <summary>Self-painted button: full crisp border on all sides and an exact
/// height that matches <see cref="FieldBox"/> at any DPI (standard flat buttons
/// drop border pixels at fractional scaling).</summary>
internal class UiButton : Button
{
    public enum Kind { Primary, Secondary, Danger, Icon, Subtle }
    private readonly Kind _kind;
    private bool _hover, _down;

    public UiButton(Kind kind)
    {
        _kind = kind;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        TabStop = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = kind == Kind.Icon ? Theme.FontIcon : (kind == Kind.Primary ? Theme.FontSemibold : Theme.Font);
        Height = kind == Kind.Icon ? 32 : 34;
        ForeColor = Theme.Text2;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaintBackground(PaintEventArgs e) { }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Color bg, fg, border = Color.Empty;
        switch (_kind)
        {
            case Kind.Primary:
                bg = _down ? Theme.AccentDown : _hover ? Theme.AccentHover : Theme.Accent; fg = Color.White; break;
            case Kind.Danger:
                bg = _hover ? Color.FromArgb(0xFC, 0xE9, 0xEA) : Color.White; fg = Theme.Red;
                border = _hover ? Theme.Red : Theme.BorderStrong; break;
            case Kind.Icon:
                bg = _hover ? Theme.SurfaceHover : Color.White; fg = ForeColor; border = Theme.BorderStrong; break;
            case Kind.Subtle:
                bg = _hover ? Theme.SurfaceHover : (Parent?.BackColor ?? Theme.Bg); fg = Theme.Text2; break;
            default:
                bg = _hover ? Theme.SurfaceHover : Color.White; fg = Theme.Text; border = Theme.BorderStrong; break;
        }
        using (var b = new SolidBrush(bg)) g.FillRectangle(b, ClientRectangle);
        if (!border.IsEmpty) { using var pen = new Pen(border, 1); g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>A stat tile: big number + caption inside a card.</summary>
internal class StatTile : Card
{
    private readonly Label _value;
    private readonly Label _caption;

    public StatTile(string caption)
    {
        Height = 74;
        Padding = new Padding(16, 12, 12, 12);

        _caption = new Label
        {
            Text = caption.ToUpperInvariant(),
            Font = Theme.FontSmall,
            ForeColor = Theme.Muted,
            BackColor = Theme.Surface,
            AutoSize = true,
            Location = new Point(16, 12)
        };
        _value = new Label
        {
            Text = "0",
            Font = Theme.FontStat,
            ForeColor = Theme.Text,
            BackColor = Theme.Surface,
            AutoSize = true,
            Location = new Point(14, 30)
        };
        Controls.Add(_caption);
        Controls.Add(_value);
    }

    public Color ValueColor { set => _value.ForeColor = value; }
    public string Value { set => _value.Text = value; }
}
