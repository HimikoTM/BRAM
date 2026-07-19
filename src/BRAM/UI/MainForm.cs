using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using RobloxAccountManager.Models;
using RobloxAccountManager.Services;

namespace RobloxAccountManager.UI;

public sealed class MainForm : Form
{
    private readonly RobloxApi _api = new();
    private readonly Vault _vault = new();
    private readonly Settings _settings = Settings.Load();
    private readonly ActivityLog _log = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new();
    private readonly Dictionary<Account, AccountCard> _cards = new();

    private bool _sweeping, _multiInit, _shownOnce;
    private string _activeGroup = "All";

    // controls
    private TableLayoutPanel _root = null!;
    private Label _vaultStatus = null!, _countLabel = null!, _emptyLabel = null!;
    private Button _lockBtn = null!;
    private StatTile _stTotal = null!, _stOnline = null!, _stInGame = null!, _stGroups = null!;
    private FieldBox _search = null!, _placeBox = null!;
    private ComboBox _groupCombo = null!;
    private CheckBox _multiCheck = null!;
    private FlowLayoutPanel _flow = null!;
    private RichTextBox _logBox = null!;

    public MainForm()
    {
        Text = "BROBLOX Account Manager";
        Font = Theme.Font;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 600);
        ClientSize = new Size(1040, 780);
        TryLoadIcon();
        BuildUi();

        _refreshTimer.Tick += (_, _) => { _ = RefreshAllAsync(false); };
        FormClosing += (_, _) => Shutdown();
    }

    private void TryLoadIcon()
    {
        try
        {
            string p = Path.Combine(AppContext.BaseDirectory, "Assets", "broblox.ico");
            if (File.Exists(p)) { Icon = new Icon(p); return; }
        }
        catch { }
        // Single-file publish has no side-by-side Assets folder — pull the icon
        // embedded in the exe (from <ApplicationIcon>) instead.
        try
        {
            string? exe = Environment.ProcessPath;
            if (exe != null) Icon = Icon.ExtractAssociatedIcon(exe);
        }
        catch { }
    }

    // ============================ UI ============================

    private void BuildUi()
    {
        _root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(16),
            BackColor = Theme.Bg,
            Visible = false
        };
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));  // header
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));  // stats
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));  // toolbar
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));  // list head
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // accounts
        _root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150)); // log

        _root.Controls.Add(BuildHeader(), 0, 0);
        _root.Controls.Add(BuildStats(), 0, 1);
        _root.Controls.Add(BuildToolbar(), 0, 2);
        _root.Controls.Add(BuildListHead(), 0, 3);
        _root.Controls.Add(BuildAccounts(), 0, 4);
        _root.Controls.Add(BuildLog(), 0, 5);

        Controls.Add(_root);
    }

    private Control BuildHeader()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };

        var left = new FlowLayoutPanel { Dock = DockStyle.Left, FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, BackColor = Theme.Bg };
        var title = new Label { Text = "BROBLOX Account Manager", Font = Theme.FontTitle, ForeColor = Theme.Text, AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        var sub = new Label { Text = "Sessions · Multi-launch · Local DPAPI vault", Font = Theme.FontSmall, ForeColor = Theme.Muted, AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
        left.Controls.Add(title);
        left.Controls.Add(sub);

        var right = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, BackColor = Theme.Bg };
        _vaultStatus = new Label { Text = "VAULT", Font = Theme.FontSmall, ForeColor = Theme.Text2, AutoSize = true, Margin = new Padding(0, 12, 12, 0) };
        var vaultBtn = Theme.Secondary("Vault password");
        vaultBtn.Width = 120; vaultBtn.Margin = new Padding(0, 12, 8, 0);
        vaultBtn.Click += (_, _) => VaultSettings();
        _lockBtn = Theme.Secondary("Lock");
        _lockBtn.Width = 70; _lockBtn.Margin = new Padding(0, 12, 0, 0); _lockBtn.Visible = false;
        _lockBtn.Click += (_, _) => LockVault();
        right.Controls.Add(_vaultStatus);
        right.Controls.Add(vaultBtn);
        right.Controls.Add(_lockBtn);

        p.Controls.Add(left);
        p.Controls.Add(right);
        return p;
    }

    private Control BuildStats()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Theme.Bg };
        for (int i = 0; i < 4; i++) t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        _stTotal = new StatTile("Accounts") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
        _stOnline = new StatTile("Online") { Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 0) };
        _stInGame = new StatTile("In game") { Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 0) };
        _stGroups = new StatTile("Groups") { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };
        _stOnline.ValueColor = Theme.Green;
        _stInGame.ValueColor = Theme.Accent;
        t.Controls.Add(_stTotal, 0, 0);
        t.Controls.Add(_stOnline, 1, 0);
        t.Controls.Add(_stInGame, 2, 0);
        t.Controls.Add(_stGroups, 3, 0);
        return t;
    }

    private Control BuildToolbar()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Theme.Bg };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var leftFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, BackColor = Theme.Bg, Margin = new Padding(0) };
        var addLogin = Theme.Primary("Add (login)");
        addLogin.Width = 108; addLogin.Margin = new Padding(0, 9, 8, 9);
        addLogin.Click += (_, _) => _ = AddByLoginAsync();
        var addCookie = Theme.Secondary("Add (cookie)");
        addCookie.Width = 108; addCookie.Margin = new Padding(0, 9, 8, 9);
        addCookie.Click += (_, _) => _ = AddByCookieAsync();
        var refreshAll = Theme.Secondary("Refresh all");
        refreshAll.Width = 96; refreshAll.Margin = new Padding(0, 9, 8, 9);
        refreshAll.Click += (_, _) => _ = RefreshAllAsync(true);
        var openClient = Theme.Secondary("Open client");
        openClient.Width = 100; openClient.Margin = new Padding(0, 9, 0, 9);
        openClient.Click += (_, _) => OpenClient();
        leftFlow.Controls.AddRange(new Control[] { addLogin, addCookie, refreshAll, openClient });

        _search = new FieldBox { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Height = 34, Margin = new Padding(10, 9, 10, 0) };
        _search.PlaceholderText = "Search accounts, groups, usernames…";
        _search.Inner.TextChanged += (_, _) => ApplyFilter();

        var rightFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, BackColor = Theme.Bg, Margin = new Padding(0) };
        _groupCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Width = 130, Font = Theme.Font,
            BackColor = Color.White, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 27, Margin = new Padding(0, 9, 8, 9)
        };
        _groupCombo.DrawItem += (_, e) =>
        {
            e.DrawBackground();
            if (e.Index >= 0)
            {
                bool sel = (e.State & DrawItemState.Selected) != 0;
                TextRenderer.DrawText(e.Graphics, _groupCombo.Items[e.Index]?.ToString() ?? "", Theme.Font,
                    new Rectangle(e.Bounds.X + 5, e.Bounds.Y, e.Bounds.Width - 5, e.Bounds.Height),
                    sel ? Color.White : Theme.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        };
        _groupCombo.SelectedIndexChanged += (_, _) => { _activeGroup = (_groupCombo.SelectedItem as string) ?? "All"; ApplyFilter(); };
        _multiCheck = new CheckBox { Text = "Multi-instance", AutoSize = true, Font = Theme.Font, ForeColor = Theme.Text, Margin = new Padding(0, 16, 10, 0), Checked = _settings.MultiInstance };
        _multiCheck.CheckedChanged += (_, _) => { if (_multiInit) ApplyMultiInstance(_multiCheck.Checked); };
        _placeBox = new FieldBox(mono: true) { Width = 96, Height = 34, Margin = new Padding(0, 9, 0, 9) };
        _placeBox.PlaceholderText = "Place ID";
        _placeBox.Text = _settings.DefaultPlaceId > 0 ? _settings.DefaultPlaceId.ToString() : "";
        _placeBox.Inner.Leave += (_, _) => SetPlaceId(_placeBox.Text);
        rightFlow.Controls.AddRange(new Control[] { _groupCombo, _multiCheck, _placeBox });

        t.Controls.Add(leftFlow, 0, 0);
        t.Controls.Add(_search, 1, 0);
        t.Controls.Add(rightFlow, 2, 0);
        return t;
    }

    private Control BuildListHead()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        _countLabel = new Label { Text = "ACCOUNTS", Font = Theme.FontSmall, ForeColor = Theme.Muted, AutoSize = true, Location = new Point(2, 6) };
        p.Controls.Add(_countLabel);
        return p;
    }

    private Control BuildAccounts()
    {
        var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoScroll = true, BackColor = Theme.Bg
        };
        _flow.SizeChanged += (_, _) => ResizeCards();
        _emptyLabel = new Label
        {
            Dock = DockStyle.Fill, Text = "No accounts yet — add one with “Add (login)”.",
            Font = Theme.Font, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleCenter, Visible = false
        };
        host.Controls.Add(_flow);
        host.Controls.Add(_emptyLabel);
        return host;
    }

    private Control BuildLog()
    {
        var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(1), Margin = new Padding(0, 8, 0, 0) };
        var head = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = Theme.Surface };
        var cap = new Label { Text = "activity.log", Font = Theme.FontSmall, ForeColor = Theme.Muted, AutoSize = true, Location = new Point(14, 8), BackColor = Theme.Surface };
        var clear = Theme.Subtle("Clear");
        clear.Width = 60; clear.Height = 24;
        clear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        clear.Location = new Point(card.Width - 76, 4);
        clear.Click += (_, _) => ClearLog();
        head.Controls.Add(cap);
        head.Controls.Add(clear);

        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Theme.Surface,
            ReadOnly = true, Font = Theme.FontMono, ScrollBars = RichTextBoxScrollBars.Vertical, Margin = new Padding(8)
        };
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(12, 6, 8, 8) };
        body.Controls.Add(_logBox);

        card.Controls.Add(body);
        card.Controls.Add(head);
        return card;
    }

    // ============================ Startup ============================

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_shownOnce) return;
        _shownOnce = true;

        _ = Task.Run(BrowserLogin.PruneStaleProfiles);

        VaultState st = _vault.Probe();
        bool ready = st switch
        {
            VaultState.DpapiOnly => true,
            VaultState.NoFile => RunSetupGate(),
            VaultState.PasswordProtected => RunUnlockGate(),
            VaultState.Corrupt => RunCorruptGate(),
            _ => false
        };

        if (!ready) { Close(); return; }

        OnUnlocked(bootRestore: st != VaultState.NoFile);
        await RefreshAllAsync(false);
    }

    private bool RunSetupGate()
    {
        using var f = new SetupForm { StartPosition = FormStartPosition.CenterScreen };
        if (f.ShowDialog(this) != DialogResult.OK) return false;
        _vault.CreateNew(f.Password);
        Log("BOOT", f.Password == null ? "Vault created (DPAPI-only)." : "Vault created with master password.", "#16A34A");
        return true;
    }

    private bool RunUnlockGate()
    {
        using var f = new UnlockForm(_vault.Unlock) { StartPosition = FormStartPosition.CenterScreen };
        if (f.ShowDialog(this) != DialogResult.OK) return false;
        Log("BOOT", $"Vault decrypted — {_vault.Accounts.Count} session(s) restored.", "#16A34A");
        return true;
    }

    private bool RunCorruptGate()
    {
        using var f = new CorruptForm { StartPosition = FormStartPosition.CenterScreen };
        if (f.ShowDialog(this) != DialogResult.OK) return false;
        _vault.CreateNew(f.Password);
        Log("VAULT", "Vault reset — previous unreadable data was discarded.", "#D97706");
        return true;
    }

    private void OnUnlocked(bool bootRestore)
    {
        _multiInit = true;
        if (_settings.MultiInstance) TryEnableMulti(false);

        LoadExistingLog();
        LoadAccounts();
        _vaultStatus.Text = _vault.Mode == "password" ? "VAULT UNLOCKED" : "DPAPI VAULT";
        _lockBtn.Visible = _vault.Mode == "password";

        if (bootRestore && _vault.Mode == "dpapi")
            Log("BOOT", $"Vault ready — {_vault.Accounts.Count} session(s) restored.", "#16A34A");

        StartRefreshTimer();
        _root.Visible = true;
    }

    // ============================ Accounts ============================

    private void LoadAccounts()
    {
        _flow.SuspendLayout();
        foreach (var c in _cards.Values) { _flow.Controls.Remove(c); c.Dispose(); }
        _cards.Clear();
        foreach (var a in _vault.Accounts) AddCard(a);
        _flow.ResumeLayout();
        RebuildGroupCombo();
        UpdateStats();
        ApplyFilter();
    }

    private void AddCard(Account a)
    {
        var card = new AccountCard(a);
        card.LaunchClicked += x => _ = LaunchAsync(x);
        card.RefreshClicked += x => _ = RefreshOneAsync(x);
        card.DeleteClicked += Delete;
        card.EditClicked += Edit;
        _cards[a] = card;
        _flow.Controls.Add(card);
        SetCardWidth(card);
    }

    private void RemoveCard(Account a)
    {
        if (_cards.TryGetValue(a, out var card))
        {
            _flow.Controls.Remove(card);
            card.Dispose();
            _cards.Remove(a);
        }
    }

    private void SetCardWidth(AccountCard card)
    {
        int w = _flow.ClientSize.Width - card.Margin.Horizontal;
        if (w > 40) card.Width = w;
    }

    private void ResizeCards()
    {
        foreach (var c in _cards.Values) SetCardWidth(c);
    }

    private async Task AddByLoginAsync()
    {
        Log("INFO", "Opening Roblox login in your browser…", "#6B7280");
        string? cookie;
        try { cookie = await BrowserLogin.CaptureCookieAsync(); }
        catch (Exception ex) { Log("WARN", "Browser login failed: " + ex.Message, "#D97706"); return; }
        if (string.IsNullOrEmpty(cookie)) { Log("INFO", "Login cancelled.", "#6B7280"); return; }
        await AddCookieCoreAsync(cookie);
    }

    private async Task AddByCookieAsync()
    {
        using var f = new CookieForm();
        if (f.ShowDialog(this) != DialogResult.OK) return;
        await AddCookieCoreAsync(f.Cookie);
    }

    private async Task AddCookieCoreAsync(string cookie)
    {
        cookie = cookie.Trim();
        (long id, string name, string display)? user;
        try { user = await _api.GetAuthenticatedUserAsync(cookie); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Add account", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        if (user is null) { MessageBox.Show(this, "That cookie isn't valid — couldn't load the account.", "Add account", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (_vault.Accounts.Any(a => a.UserId == user.Value.id)) { MessageBox.Show(this, $"{user.Value.name} is already in the vault.", "Add account", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }

        var acc = new Account { SecurityCookie = cookie, UserId = user.Value.id, Username = user.Value.name, Label = user.Value.display };
        _vault.Accounts.Add(acc);
        _vault.Save();
        AddCard(acc);
        RebuildGroupCombo();
        UpdateStats();
        ApplyFilter();
        Log("ADD", $"Added {acc.Username}.", "#16A34A");
        await RefreshAccountAsync(acc, true);
    }

    private async Task LaunchAsync(Account a)
    {
        if (a.IsBusy) return;
        a.IsBusy = true;
        a.Status = "Authenticating…";
        UpdateCard(a);
        try
        {
            if (_settings.MultiInstance && !Launcher.MultiInstanceEnabled) TryEnableMulti(true);
            string? ticket = await _api.GetAuthTicketAsync(a.SecurityCookie);
            if (ticket is null)
            {
                a.Status = "Login expired — re-add";
                Log("WARN", $"{a.Username}: saved login expired.", "#D97706");
                MessageBox.Show(this, $"Couldn't get an auth ticket for {a.Username}. The saved login is likely expired — remove and re-add it.", "Launch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            long placeId = ParsePlaceId(_placeBox.Text) ?? _settings.DefaultPlaceId;
            Launcher.LaunchAuthenticated(ticket, placeId);
            a.LastUsed = DateTime.Now;
            a.Status = "Launched";
            _vault.Save();
            Log("LAUNCH", placeId > 0 ? $"Launched {a.Username} → place {placeId}." : $"Launched {a.Username}.", "#16A34A");
        }
        catch (Exception ex) { Log("WARN", "Launch failed: " + ex.Message, "#D97706"); }
        finally { a.IsBusy = false; UpdateCard(a); }
    }

    private void OpenClient()
    {
        if (_settings.MultiInstance && !Launcher.MultiInstanceEnabled) TryEnableMulti(true);
        Launcher.LaunchManual();
        Log("INFO", "Opened a clean Roblox client.", "#6B7280");
    }

    private async Task RefreshOneAsync(Account a) => await RefreshAccountAsync(a, true);

    private async Task RefreshAllAsync(bool manual)
    {
        if (!_vault.IsUnlocked) return;
        if (_sweeping) { if (manual) Log("INFO", "A refresh is already in progress…", "#6B7280"); return; }
        _sweeping = true;
        try
        {
            foreach (var a in _vault.Accounts.ToList())
                await RefreshAccountAsync(a, false);
            _vault.Save();
        }
        finally { _sweeping = false; }
    }

    private async Task RefreshAccountAsync(Account a, bool save)
    {
        if (a.IsBusy) return;
        a.IsBusy = true;
        a.Status = "Checking…";
        UpdateCard(a);
        try
        {
            var user = await _api.GetAuthenticatedUserAsync(a.SecurityCookie);
            if (user is null) { a.Status = "Logged out — re-add"; return; }

            a.UserId = user.Value.id;
            a.Username = user.Value.name;
            if (string.IsNullOrEmpty(a.Label)) a.Label = user.Value.display;

            string? avatar = await _api.GetAvatarHeadshotAsync(a.UserId);
            if (!string.IsNullOrEmpty(avatar)) a.AvatarUrl = avatar!;

            int presence = await _api.GetPresenceAsync(a.UserId, a.SecurityCookie);
            a.Status = presence switch { 0 => "Offline", 1 => "Online", 2 => "In game", 3 => "In Studio", _ => "Unknown" };
            if (save) _vault.Save();
        }
        catch { a.Status = "Error checking"; }
        finally { a.IsBusy = false; UpdateCard(a); UpdateStats(); }
    }

    private void Delete(Account a)
    {
        var r = MessageBox.Show(this, $"Remove {a.Username}? This only removes it from this manager — your Roblox account is untouched.",
            "Remove account", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return;
        _vault.Accounts.Remove(a);
        _vault.Save();
        RemoveCard(a);
        RebuildGroupCombo();
        UpdateStats();
        ApplyFilter();
        Log("WARN", $"Removed {a.Username} from the vault.", "#D97706");
    }

    private void Edit(Account a)
    {
        using var f = new EditAccountForm(a);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        _vault.Save();
        UpdateCard(a);
        RebuildGroupCombo();
        UpdateStats();
        ApplyFilter();
        Log("EDIT", $"Updated {a.Username}.", "#6B7280");
    }

    private void UpdateCard(Account a)
    {
        if (_cards.TryGetValue(a, out var card)) card.Bind();
    }

    // ============================ Vault / settings ============================

    private void LockVault()
    {
        StopRefreshTimer();
        _vault.Lock();
        foreach (var c in _cards.Values) { _flow.Controls.Remove(c); c.Dispose(); }
        _cards.Clear();
        _root.Visible = false;

        if (!RunUnlockGate()) { Close(); return; }
        OnUnlocked(bootRestore: false);
        _ = RefreshAllAsync(false);
    }

    private void VaultSettings()
    {
        using var f = new VaultSettingsForm(_vault.Mode == "password");
        if (f.ShowDialog(this) != DialogResult.OK || f.Action == VaultAction.Cancel) return;
        _vault.ChangeMasterPassword(f.Action == VaultAction.Remove ? null : f.Password);
        _vaultStatus.Text = _vault.Mode == "password" ? "VAULT UNLOCKED" : "DPAPI VAULT";
        _lockBtn.Visible = _vault.Mode == "password";
        Log("VAULT", f.Action == VaultAction.Remove ? "Master password removed (DPAPI-only)." : "Master password updated.", "#6B7280");
    }

    private void ApplyMultiInstance(bool enabled)
    {
        _settings.MultiInstance = enabled;
        _settings.Save();
        if (enabled) TryEnableMulti(true);
        else { Launcher.DisableMultiInstance(); Log("INFO", "Multi-instance OFF.", "#6B7280"); }
    }

    private void TryEnableMulti(bool announce)
    {
        if (Launcher.EnableMultiInstance())
        {
            if (announce) Log("MULTI", "Multi-instance ON — holding ROBLOX_singletonEvent.", "#D97706");
            return;
        }
        if (announce)
        {
            Log("WARN", "Multi-instance: couldn't acquire the singleton handle — is Roblox already running?", "#D97706");
            MessageBox.Show(this, "Couldn't enable multi-instance — close every Roblox window and try again.", "Multi-instance", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void SetPlaceId(string text)
    {
        _settings.DefaultPlaceId = ParsePlaceId(text) ?? 0;
        _settings.Save();
    }

    private static long? ParsePlaceId(string? text)
        => long.TryParse((text ?? "").Trim(), out long p) && p > 0 ? p : null;

    // ============================ Filtering / stats ============================

    private void RebuildGroupCombo()
    {
        var names = new List<string> { "All" };
        names.AddRange(_vault.Accounts.Select(a => a.Group).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        if (!names.Contains(_activeGroup)) _activeGroup = "All";

        _groupCombo.BeginUpdate();
        _groupCombo.Items.Clear();
        foreach (var n in names) _groupCombo.Items.Add(n);
        _groupCombo.SelectedItem = _activeGroup;
        _groupCombo.EndUpdate();
    }

    private void ApplyFilter()
    {
        string q = _search.Text.Trim().ToLowerInvariant();
        int shown = 0;
        _flow.SuspendLayout();
        foreach (var (a, card) in _cards.Select(kv => (kv.Key, kv.Value)))
        {
            bool ok = (_activeGroup == "All" || a.Group == _activeGroup);
            if (ok && q.Length > 0)
            {
                string hay = ((a.Label ?? "") + " " + (a.Username ?? "") + " " + (a.Group ?? "") + " " + (a.Status ?? "")).ToLowerInvariant();
                ok = hay.Contains(q);
            }
            card.Visible = ok;
            if (ok) shown++;
        }
        _flow.ResumeLayout();
        _countLabel.Text = $"ACCOUNTS  [ {shown} ]";
        _emptyLabel.Visible = _cards.Count == 0;
        if (_emptyLabel.Visible) _emptyLabel.BringToFront();
    }

    private void UpdateStats()
    {
        var all = _vault.Accounts;
        _stTotal.Value = all.Count.ToString();
        _stOnline.Value = all.Count(a => a.Status is "Online" or "In game").ToString();
        _stInGame.Value = all.Count(a => a.Status == "In game").ToString();
        _stGroups.Value = all.Select(a => a.Group).Distinct().Count().ToString();
    }

    // ============================ Log ============================

    private void LoadExistingLog()
    {
        _logBox.Clear();
        foreach (var e in _log.Entries) AppendLog(e);
    }

    private void Log(string tag, string text, string color)
    {
        LogEntry e = _log.Add(tag, text, color);
        AppendLog(e);
    }

    private void AppendLog(LogEntry e)
    {
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = Theme.Muted;
        _logBox.AppendText(e.Time + "  ");
        _logBox.SelectionColor = SafeColor(e.Color);
        _logBox.AppendText(e.Tag + "  ");
        _logBox.SelectionColor = Theme.Text2;
        _logBox.AppendText(e.Text + "\n");
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.ScrollToCaret();
    }

    private void ClearLog()
    {
        _log.Clear();
        _logBox.Clear();
    }

    private static Color SafeColor(string hex)
    {
        try { return ColorTranslator.FromHtml(hex); } catch { return Theme.Text2; }
    }

    // ============================ Timer / shutdown ============================

    private void StartRefreshTimer()
    {
        StopRefreshTimer();
        int seconds = _settings.AutoRefreshSeconds;
        if (seconds <= 0) return;
        _refreshTimer.Interval = seconds * 1000;
        _refreshTimer.Start();
    }

    private void StopRefreshTimer() => _refreshTimer.Stop();

    private void Shutdown()
    {
        StopRefreshTimer();
        _settings.Save();
        Launcher.DisableMultiInstance();
        _api.Dispose();
    }
}
