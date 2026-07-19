using System;
using System.Drawing;
using System.Windows.Forms;
using RobloxAccountManager.Models;

namespace RobloxAccountManager.UI;

/// <summary>One account row: avatar, name/meta, and quick actions.</summary>
internal sealed class AccountCard : Card
{
    // Segoe MDL2 Assets glyphs, built from code points to keep the source pure ASCII.
    private static readonly string GlyphEdit = char.ConvertFromUtf32(0xE70F);    // pencil
    private static readonly string GlyphRefresh = char.ConvertFromUtf32(0xE72C); // refresh
    private static readonly string GlyphTrash = char.ConvertFromUtf32(0xE74D);   // trash
    private static readonly string Bullet = char.ConvertFromUtf32(0x25CF);       // ●
    private static readonly string MiddleDot = char.ConvertFromUtf32(0x00B7);    // ·

    public Account Account { get; }

    public event Action<Account>? LaunchClicked;
    public event Action<Account>? EditClicked;
    public event Action<Account>? RefreshClicked;
    public event Action<Account>? DeleteClicked;

    private readonly AvatarControl _avatar;
    private readonly Label _name;
    private readonly Chip _group;
    private readonly Label _status;
    private readonly Label _lastUsed;
    private readonly Button _launch;
    private readonly Button _edit;
    private readonly Button _refresh;
    private readonly Button _delete;

    private string _avatarUrl = "";

    public AccountCard(Account account)
    {
        Account = account;
        Height = 74;
        Margin = new Padding(0, 0, 0, 8);
        Cursor = Cursors.Hand;

        _avatar = new AvatarControl { RingColor = Theme.Surface };

        _name = new Label
        {
            AutoSize = false, AutoEllipsis = true, Font = Theme.FontSemibold,
            ForeColor = Theme.Text, BackColor = Theme.Surface, Height = 20
        };
        _group = new Chip();
        _status = new Label { AutoSize = true, Font = Theme.FontSmall, BackColor = Theme.Surface };
        _lastUsed = new Label { AutoSize = true, Font = Theme.FontSmall, ForeColor = Theme.Muted, BackColor = Theme.Surface };

        _launch = Theme.Primary("Launch");
        _launch.Width = 84;
        _edit = Theme.IconBtn(GlyphEdit);
        _refresh = Theme.IconBtn(GlyphRefresh);
        _delete = Theme.IconBtn(GlyphTrash, Theme.Red);

        _launch.Click += (_, _) => LaunchClicked?.Invoke(Account);
        _edit.Click += (_, _) => EditClicked?.Invoke(Account);
        _refresh.Click += (_, _) => RefreshClicked?.Invoke(Account);
        _delete.Click += (_, _) => DeleteClicked?.Invoke(Account);

        Controls.AddRange(new Control[] { _avatar, _name, _group, _status, _lastUsed, _launch, _edit, _refresh, _delete });

        foreach (Control c in new Control[] { this, _avatar, _name, _status, _lastUsed })
            c.DoubleClick += (_, _) => EditClicked?.Invoke(Account);

        Bind();
    }

    public void Bind()
    {
        _name.Text = string.IsNullOrWhiteSpace(Account.Label) ? Account.Username : $"{Account.Label}  ({Account.Username})";
        _group.Text = string.IsNullOrWhiteSpace(Account.Group) ? "General" : Account.Group;

        Color sc = Theme.StatusColor(Account.Status);
        _status.Text = Bullet + "  " + Account.Status;
        _status.ForeColor = sc;
        _lastUsed.Text = MiddleDot + "  " + Rel(Account.LastUsed);

        string initial = string.IsNullOrWhiteSpace(Account.Label) ? Account.Username : Account.Label;
        initial = string.IsNullOrEmpty(initial) ? "?" : initial.Substring(0, 1).ToUpperInvariant();
        _avatar.Set(initial, NameColor(Account.Username), sc);

        DoLayout();
        LoadAvatar();
    }

    private async void LoadAvatar()
    {
        if (Account.AvatarUrl == _avatarUrl) return;
        _avatarUrl = Account.AvatarUrl;
        if (string.IsNullOrEmpty(_avatarUrl)) { _avatar.Image = null; return; }
        var img = await AvatarCache.GetAsync(_avatarUrl);
        if (img != null && Account.AvatarUrl == _avatarUrl) _avatar.Image = img;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        DoLayout();
    }

    private void DoLayout()
    {
        if (_avatar is null || _launch is null) return; // guard: fires during base ctor sizing
        int h = Height;
        _avatar.Location = new Point(14, (h - _avatar.Height) / 2);

        int right = Width - 14;
        _delete.Location = new Point(right - _delete.Width, (h - _delete.Height) / 2);
        _refresh.Location = new Point(_delete.Left - 8 - _refresh.Width, (h - _refresh.Height) / 2);
        _edit.Location = new Point(_refresh.Left - 8 - _edit.Width, (h - _edit.Height) / 2);
        _launch.Location = new Point(_edit.Left - 8 - _launch.Width, (h - _launch.Height) / 2);

        int textLeft = _avatar.Right + 14;
        int textRight = _launch.Left - 14;
        _name.Location = new Point(textLeft, 15);
        _name.Width = Math.Max(40, textRight - textLeft);

        _group.Location = new Point(textLeft, 40);
        _status.Location = new Point(_group.Right + 10, 40);
        _lastUsed.Location = new Point(_status.Right + 8, 41);
    }

    private static string Rel(DateTime? dt)
    {
        if (dt is null) return "never launched";
        double s = (DateTime.Now - dt.Value).TotalSeconds;
        if (s < 45) return "just now";
        double m = s / 60; if (m < 60) return $"{(int)m}m ago";
        double h = m / 60; if (h < 24) return $"{(int)h}h ago";
        double d = h / 24; if (d < 2) return "yesterday";
        if (d < 30) return $"{(int)d}d ago";
        return dt.Value.ToShortDateString();
    }

    private static Color NameColor(string name)
    {
        if (string.IsNullOrEmpty(name)) name = "?";
        int hue = 0;
        foreach (char c in name) hue = (hue * 31 + c) % 360;
        return FromHsl(hue, 0.52, 0.55);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        h /= 360.0;
        double r, g, b;
        if (s == 0) { r = g = b = l; }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = Hue(p, q, h + 1.0 / 3);
            g = Hue(p, q, h);
            b = Hue(p, q, h - 1.0 / 3);
        }
        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    private static double Hue(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2) return q;
        if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
        return p;
    }
}
