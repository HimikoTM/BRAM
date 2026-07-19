using System;
using System.Drawing;
using System.Windows.Forms;
using RobloxAccountManager.Models;

namespace RobloxAccountManager.UI;

/// <summary>Shared helpers for building small light-themed dialogs.</summary>
internal static class Dlg
{
    public static void Style(Form f, int w, int h)
    {
        f.Font = Theme.Font;
        f.BackColor = Theme.Bg;
        f.ForeColor = Theme.Text;
        f.FormBorderStyle = FormBorderStyle.FixedDialog;
        f.MaximizeBox = false;
        f.MinimizeBox = false;
        f.ShowInTaskbar = false;
        f.StartPosition = FormStartPosition.CenterParent;
        f.ClientSize = new Size(w, h);
    }

    public static TextBox Text(bool pw = false)
    {
        var t = new TextBox { Font = Theme.Font, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
        if (pw) t.UseSystemPasswordChar = true;
        return t;
    }

    public static Label Title(string s) => new() { Text = s, Font = Theme.FontTitle, ForeColor = Theme.Text, AutoSize = true };
    public static Label Cap(string s) => new() { Text = s, Font = Theme.FontSmall, ForeColor = Theme.Muted, AutoSize = true };
    public static Label Para(string s, int w) => new() { Text = s, Font = Theme.Font, ForeColor = Theme.Text2, MaximumSize = new Size(w, 0), AutoSize = true };
    public static Label Error() => new() { Text = "", Font = Theme.FontSmall, ForeColor = Theme.Red, AutoSize = false, Height = 16 };
}

/// <summary>First run: choose a master password or skip to DPAPI-only.</summary>
internal sealed class SetupForm : Form
{
    public string? Password { get; private set; }

    public SetupForm()
    {
        Text = "Secure your vault";
        Dlg.Style(this, 372, 250);

        var title = Dlg.Title("Secure your vault");
        title.Location = new Point(24, 22);
        var para = Dlg.Para("Set a master password to encrypt your saved sessions, or skip to use Windows DPAPI only (no password, tied to this Windows account).", 320);
        para.Location = new Point(24, 56);
        var box = Dlg.Text(true);
        box.SetBounds(24, 128, 324, 26);
        var err = Dlg.Error();
        err.SetBounds(24, 160, 324, 16);

        var set = Theme.Primary("Set password");
        set.SetBounds(24, 188, 150, 34);
        var skip = Theme.Secondary("Skip (DPAPI)");
        skip.SetBounds(182, 188, 130, 34);

        set.Click += (_, _) =>
        {
            if (box.Text.Length < 4) { err.Text = "Use at least 4 characters, or Skip."; return; }
            Password = box.Text;
            DialogResult = DialogResult.OK;
        };
        skip.Click += (_, _) => { Password = null; DialogResult = DialogResult.OK; };
        AcceptButton = set;

        Controls.AddRange(new Control[] { title, para, box, err, set, skip });
    }
}

/// <summary>Unlock an existing password-protected vault.</summary>
internal sealed class UnlockForm : Form
{
    public UnlockForm(Func<string, bool> tryUnlock)
    {
        Text = "Vault locked";
        Dlg.Style(this, 372, 210);

        var title = Dlg.Title("Enter master password");
        title.Location = new Point(24, 22);
        var box = Dlg.Text(true);
        box.SetBounds(24, 70, 324, 26);
        var err = Dlg.Error();
        err.SetBounds(24, 102, 324, 16);
        var unlock = Theme.Primary("Unlock");
        unlock.SetBounds(24, 130, 324, 36);

        unlock.Click += (_, _) =>
        {
            if (tryUnlock(box.Text)) { DialogResult = DialogResult.OK; return; }
            err.Text = "Wrong password — try again.";
            box.SelectAll();
            box.Focus();
        };
        AcceptButton = unlock;

        Controls.AddRange(new Control[] { title, box, err, unlock });
    }
}

/// <summary>Recovery screen for an undecryptable vault.</summary>
internal sealed class CorruptForm : Form
{
    public string? Password { get; private set; }

    public CorruptForm()
    {
        Text = "Vault unreadable";
        Dlg.Style(this, 440, 280);

        var cap = Dlg.Cap("VAULT UNREADABLE");
        cap.ForeColor = Theme.Amber;
        cap.Location = new Point(24, 20);
        var title = Dlg.Title("Couldn't decrypt your vault");
        title.Location = new Point(24, 40);
        var para = Dlg.Para("A saved vault exists but can't be decrypted on this Windows account — it may belong to a different user/PC, or be corrupted. Your file is left untouched. Start a fresh vault (this erases the unreadable file), or close to restore a backup.", 392);
        para.Location = new Point(24, 74);
        var box = Dlg.Text(true);
        box.SetBounds(24, 176, 392, 26);
        var err = Dlg.Error();
        err.SetBounds(24, 208, 392, 16);
        var reset = Theme.Primary("Start fresh");
        reset.SetBounds(24, 234, 150, 34);

        reset.Click += (_, _) =>
        {
            if (box.Text.Length > 0 && box.Text.Length < 4) { err.Text = "Use at least 4 characters, or leave blank."; return; }
            Password = box.Text.Length == 0 ? null : box.Text;
            DialogResult = DialogResult.OK;
        };

        Controls.AddRange(new Control[] { cap, title, para, box, err, reset });
    }
}

/// <summary>Add an account by pasting its .ROBLOSECURITY cookie.</summary>
internal sealed class CookieForm : Form
{
    public string Cookie { get; private set; } = "";

    public CookieForm()
    {
        Text = "Add by cookie";
        Dlg.Style(this, 460, 300);

        var title = Dlg.Title("Paste .ROBLOSECURITY");
        title.Location = new Point(24, 22);
        var para = Dlg.Para("Paste the .ROBLOSECURITY cookie value of an account you own. It's stored encrypted and never leaves this machine except to Roblox.", 412);
        para.Location = new Point(24, 56);
        var box = new TextBox
        {
            Multiline = true, Font = Theme.FontMono, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White, ScrollBars = ScrollBars.Vertical
        };
        box.SetBounds(24, 116, 412, 110);

        var add = Theme.Primary("Add account");
        add.SetBounds(24, 240, 140, 34);
        var cancel = Theme.Secondary("Cancel");
        cancel.SetBounds(172, 240, 100, 34);

        add.Click += (_, _) =>
        {
            string c = box.Text.Trim();
            if (c.Length == 0) return;
            Cookie = c;
            DialogResult = DialogResult.OK;
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[] { title, para, box, add, cancel });
    }
}

internal enum VaultAction { Cancel, Set, Remove }

/// <summary>Set / change / remove the master password.</summary>
internal sealed class VaultSettingsForm : Form
{
    public VaultAction Action { get; private set; } = VaultAction.Cancel;
    public string? Password { get; private set; }

    public VaultSettingsForm(bool isPassword)
    {
        Text = "Vault security";
        Dlg.Style(this, 400, 240);

        var title = Dlg.Title(isPassword ? "Change master password" : "Set a master password");
        title.Location = new Point(24, 22);
        var para = Dlg.Para(isPassword
            ? "Enter a new password to change it, or remove it to fall back to Windows DPAPI only."
            : "Add a master password on top of Windows DPAPI. You'll enter it each time the app opens.", 352);
        para.Location = new Point(24, 54);
        var box = Dlg.Text(true);
        box.SetBounds(24, 118, 352, 26);
        var err = Dlg.Error();
        err.SetBounds(24, 150, 352, 16);

        var save = Theme.Primary(isPassword ? "Change" : "Set password");
        save.SetBounds(24, 178, 130, 34);
        int nextX = 162;
        if (isPassword)
        {
            var remove = Theme.Secondary("Remove");
            remove.SetBounds(nextX, 178, 100, 34);
            remove.Click += (_, _) => { Action = VaultAction.Remove; DialogResult = DialogResult.OK; };
            Controls.Add(remove);
            nextX += 108;
        }
        var cancel = Theme.Secondary("Cancel");
        cancel.SetBounds(nextX, 178, 90, 34);

        save.Click += (_, _) =>
        {
            if (box.Text.Length < 4) { err.Text = "Use at least 4 characters."; return; }
            Action = VaultAction.Set;
            Password = box.Text;
            DialogResult = DialogResult.OK;
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
        AcceptButton = save;

        Controls.AddRange(new Control[] { title, para, box, err, save, cancel });
    }
}

/// <summary>Edit label / group / notes and copy the cookie.</summary>
internal sealed class EditAccountForm : Form
{
    private readonly Account _a;

    public EditAccountForm(Account a)
    {
        _a = a;
        Text = "Edit account";
        Dlg.Style(this, 400, 400);

        var title = Dlg.Title(a.Username);
        title.MaximumSize = new Size(352, 0);
        title.AutoEllipsis = true;
        title.Location = new Point(24, 20);
        var sub = Dlg.Cap($"USER ID {a.UserId}");
        sub.Location = new Point(24, 50);

        var lblLabel = Dlg.Cap("LABEL"); lblLabel.Location = new Point(24, 80);
        var label = Dlg.Text(); label.Text = a.Label; label.SetBounds(24, 98, 352, 26);
        var lblGroup = Dlg.Cap("GROUP"); lblGroup.Location = new Point(24, 132);
        var group = Dlg.Text(); group.Text = a.Group; group.SetBounds(24, 150, 352, 26);
        var lblNotes = Dlg.Cap("NOTES"); lblNotes.Location = new Point(24, 184);
        var notes = new TextBox
        {
            Multiline = true, Font = Theme.Font, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White, ScrollBars = ScrollBars.Vertical, Text = a.Notes
        };
        notes.SetBounds(24, 202, 352, 96);

        var copy = Theme.Secondary("Copy cookie");
        copy.SetBounds(24, 320, 130, 34);
        var save = Theme.Primary("Save");
        save.SetBounds(276, 320, 100, 34);
        var cancel = Theme.Secondary("Cancel");
        cancel.SetBounds(168, 320, 100, 34);

        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(a.SecurityCookie); copy.Text = "Copied!"; }
            catch { copy.Text = "Clipboard busy"; }
        };
        save.Click += (_, _) =>
        {
            a.Label = label.Text.Trim();
            string g = group.Text.Trim();
            a.Group = string.IsNullOrWhiteSpace(g) ? "General" : g;
            a.Notes = notes.Text;
            DialogResult = DialogResult.OK;
        };
        cancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Controls.AddRange(new Control[] { title, sub, lblLabel, label, lblGroup, group, lblNotes, notes, copy, cancel, save });
    }
}
