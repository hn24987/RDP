using System.Diagnostics;
using System.Net.Sockets;
using System.Media;
using System.Text.Json;

namespace RdpLauncher;

public sealed class LauncherForm : Form
{
    readonly TextBox repository = new() { PlaceholderText = "https://github.com/YOUR-USERNAME/RDP", Width = 480 };
    readonly Button updates = Theme.Button("Check for updates");
    readonly Button settings = Theme.Button("Settings");
    readonly Button receive = Theme.Button("Receive files");
    readonly Label remaining = new() { Text = "--:--:--", AutoSize = true, Font = new Font("Consolas", 26, FontStyle.Bold), ForeColor = Theme.Accent };
    readonly Label address = new() { Text = "Not connected", AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Theme.Text };
    readonly Label sessionBadge = new() { Text = "No active session", AutoSize = true, ForeColor = Theme.Muted };
    readonly Panel scroll = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };
    Panel setupCard = null!, sessionCard = null!, transferCard = null!, activityCard = null!;
    readonly List<(Button Button, string Section)> navigation = new();
    readonly System.Windows.Forms.Timer countdownTimer = new() { Interval = 1000 };
    readonly SessionCountdown countdown = new();
    readonly CancellationTokenSource lifetime = new();
    readonly Dictionary<Control, (string Key, object[] Args)> captions = new();
    bool receiving;
    bool warningOpen;
    bool resourcesDisposed;
    readonly TextBox sessionPassword = new() { UseSystemPasswordChar = true, MaxLength = 127, PlaceholderText = "Optional — default: 123456" };
    string token = "";
    readonly Label forkStatus = new() { AutoSize = true, MaximumSize = new Size(880, 0) };
    readonly Label tokenStatus = new() { Text = "GitHub token not configured", AutoSize = true };
    readonly Button changeToken = Theme.Button("Add / change token");
    readonly Button forgetToken = Theme.Button("Forget token");
    readonly NumericUpDown duration = new() { Minimum = 10, Maximum = 300, Value = 60, Width = 85 };
    readonly TextBox runId = new() { Width = 180 };
    readonly Button check = Theme.Button("Check setup");
    readonly Button start = Theme.Button("Get RDP", primary: true);
    readonly Button resume = Theme.Button("Resume run");
    readonly Button stopWatch = Theme.Button("Stop monitoring");
    readonly Button cancel = Theme.Button("Stop workflow", danger: true);
    readonly Button connect = Theme.Button("Open Remote Desktop", primary: true);
    readonly Button copy = Theme.Button("Copy IP");
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    readonly Label connection = new() { Text = "No RDP session connected.", AutoSize = true, MaximumSize = new Size(830, 0) };
    readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RdpLauncher", "session-v2.json");
    string? pendingRequest;
    bool customPasswordPending;
    string? sessionRepository;
    string? currentIp;
    CancellationTokenSource? operation;
    bool busy;
    sealed record SavedSession(string Repository, string RunId, string? RequestId, bool CustomPasswordPending = false);
    sealed record SavedState(string Repository, Dictionary<string, SavedSession> Sessions);
    Dictionary<string, SavedSession> sessions = new(StringComparer.OrdinalIgnoreCase);

    public LauncherForm()
    {
        Text = "RDP Launcher";
        Size = new Size(1320, 940);
        MinimumSize = new Size(1120, 780);
        Font = new Font("Segoe UI", 10);
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        copy.Enabled = connect.Enabled = stopWatch.Enabled = false;
        BuildDashboard();
        Theme.Apply(this);
        RegisterCaptions(this);
        ApplyLanguage();
        countdownTimer.Tick += (_, _) => TickCountdown();
        countdownTimer.Start();
        updates.Click += async (_, _) => await CheckUpdates();
        settings.Click += (_, _) =>
        {
            using var dialog = new SettingsForm();
            if (dialog.ShowDialog(this) == DialogResult.OK) ApplyLanguage();
        };
        receive.Click += async (_, _) => await ReceiveFiles();
        changeToken.Click += (_, _) => AskForToken();
        forgetToken.Click += (_, _) =>
        {
            if (MessageBox.Show(L.T("Delete the saved token from this folder?"), L.T("Forget token"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { TokenStore.Delete(); token = ""; Caption(tokenStatus, "GitHub token not configured"); UpdateStart(); }
            catch (Exception ex) { Write(L.T("Could not delete token file: ") + ex.Message); }
        };
        repository.TextChanged += (_, _) => UpdateStart();
        repository.Validated += (_, _) => { if (ValidFork()) { try { SelectFork(); Save(); } catch (Exception ex) { Write(ex.Message); } } };
        Shown += async (_, _) =>
        {
            try
            {
                if (TokenStore.Exists) { token = TokenStore.Load(); Caption(tokenStatus, "Saved GitHub token loaded"); }
            }
            catch { Write(L.T("The saved token cannot be decrypted. Enter it again for this Windows account.")); }
            if (string.IsNullOrWhiteSpace(token)) AskForToken();
            UpdateStart();
            if (!string.IsNullOrWhiteSpace(token) && ValidFork())
                await Execute(async (api, ct) => { await Preflight(api, ct); Write(L.T("Preflight complete. Click Get RDP when ready.")); });
            else repository.Focus();
        };
        check.Click += async (_, _) => await Execute(async (api, ct) => { await Preflight(api, ct); Write(L.T("Preflight complete. No workflow has been started.")); });
        start.Click += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(runId.Text) || pendingRequest is not null)
            {
                MessageBox.Show(L.T("A previous session is saved. Use Resume run first. After it ends, clear Run ID before starting a new session."));
                if (string.IsNullOrWhiteSpace(runId.Text) && MessageBox.Show(L.T("The previous request may already have created a machine. Have you checked Actions and still want to discard the saved request ID?"), L.T("Discard saved request"), MessageBoxButtons.YesNo) == DialogResult.Yes) { pendingRequest = null; Save(); }
                return;
            }
            await Execute(async (api, ct) =>
            {
                var branch = await Preflight(api, ct);
                sessionRepository = RepositoryAddress.Parse(repository.Text);
                pendingRequest = Guid.NewGuid().ToString("N").ToUpperInvariant();
                customPasswordPending = sessionPassword.TextLength > 0;
                Save(); // Persist correlation BEFORE dispatch: a lost response must not cause duplicate dispatch.
                Write(L.F("Dispatching session {0}…", pendingRequest));
                var passwordSecret = await api.PreparePassword(pendingRequest, sessionPassword.Text, ct);
                await api.Dispatch(branch, pendingRequest, (int)duration.Value, ct, passwordSecret);
                Write(L.T("GitHub accepted the request. Finding your run…"));
                var id = await Discover(api, ct);
                await Monitor(api, id, ct);
            });
        };
        resume.Click += async (_, _) => await Execute(async (api, ct) =>
        {
            if (pendingRequest is not null && string.IsNullOrWhiteSpace(runId.Text) && sessionRepository != RepositoryAddress.Parse(repository.Text))
                throw new InvalidOperationException(L.T("This pending request belongs to a different fork. Restore its fork URL before resuming."));
            await CheckTailscale(ct);
            long id;
            if (string.IsNullOrWhiteSpace(runId.Text) && pendingRequest is not null) id = await Discover(api, ct);
            else if (!long.TryParse(runId.Text, out id) || id <= 0) throw new InvalidOperationException(L.T("Enter the Run ID from your GitHub Actions URL: /actions/runs/123456."));
            await Monitor(api, id, ct);
        });
        stopWatch.Click += (_, _) => operation?.Cancel();
        cancel.Click += async (_, _) =>
        {
            if (!long.TryParse(runId.Text, out var id) || id <= 0) { MessageBox.Show(L.T("Enter a Run ID to stop.")); return; }
            if (MessageBox.Show(L.F("Stop workflow {0}? This will disconnect the RDP session.", id), L.T("Stop session"), MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try
            {
                using var api = NewClient();
                await api.Cancel(id, CancellationToken.None);
                operation?.Cancel();
                ClearConnection();
                Write(L.T("Stop requested. Check the final status in Actions / logs."));
            }
            catch (Exception ex) { Write(ex.Message); }
        };
        copy.Click += (_, _) => { if (currentIp is not null) Clipboard.SetText(currentIp); };
        connect.Click += (_, _) =>
        {
            try { if (currentIp is not null) Process.Start(new ProcessStartInfo("mstsc.exe") { UseShellExecute = true, ArgumentList = { $"/v:{currentIp}" } }); }
            catch (Exception ex) { Write(L.T("Could not open Remote Desktop: ") + ex.Message); }
        };
        FormClosing += (_, e) =>
        {
            if (busy && MessageBox.Show(L.T("Closing the app stops monitoring only. The workflow keeps running; you can resume monitoring later. Close the app?"), L.T("Close"), MessageBoxButtons.YesNo) != DialogResult.Yes)
            { e.Cancel = true; return; }
            operation?.Cancel();
        };
        try
        {
            if (File.Exists(settingsPath))
            {
                var saved = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(settingsPath));
                if (saved is not null)
                {
                    sessions = new Dictionary<string, SavedSession>(saved.Sessions, StringComparer.OrdinalIgnoreCase);
                    repository.Text = saved.Repository;
                    SelectFork();
                }
            }
        }
        catch { Write(L.T("Could not load the previous session. You can enter a Run ID manually.")); }
        Write(L.T("First run: select your repository, enable Actions and add TAILSCALE_AUTH_KEY. Leave the session password blank to use 123456."));
        UpdateStart();
    }
    Label LabelText(string text, float size = 10, bool bold = false, Color? color = null)
    {
        var label = Theme.Label(text, size, bold, color ?? Theme.Text);
        label.Margin = new Padding(0, 0, 0, 6);
        return label;
    }
    static TableLayoutPanel Stack()
    {
        return new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
    }
    static void Item(TableLayoutPanel stack, Control control, float height = 0)
    {
        var index = stack.RowCount++;
        stack.RowStyles.Add(height > 0 ? new RowStyle(SizeType.Absolute, height) : new RowStyle(SizeType.AutoSize));
        control.Dock = DockStyle.Fill;
        stack.Controls.Add(control, 0, index);
    }
    static void Spacer(TableLayoutPanel stack)
    {
        stack.RowCount++;
        stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
    }
    static Panel Input(Control control)
    {
        var wrap = new Panel { Height = 44, BackColor = Theme.Raised, Padding = new Padding(12, 10, 12, 8), Margin = new Padding(0, 3, 0, 10) };
        control.Dock = DockStyle.Fill;
        if (control is TextBox text) { text.BorderStyle = BorderStyle.None; text.Font = new Font("Segoe UI", 10.5f); }
        wrap.Controls.Add(control);
        return wrap;
    }
    void BuildDashboard()
    {
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Theme.Bg };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 212));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(shell);
        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 13, 24), Padding = new Padding(20, 26, 20, 20) };
        shell.Controls.Add(sidebar, 0, 0);
        var side = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 10, BackColor = Color.Transparent };
        for (int i = 0; i < 10; i++) side.RowStyles.Add(new RowStyle(i == 6 ? SizeType.Percent : SizeType.AutoSize, i == 6 ? 100 : 0));
        sidebar.Controls.Add(side);
        var brand = LabelText("RDP", 30, true, Theme.Accent); brand.Margin = Padding.Empty;
        side.Controls.Add(brand, 0, 0);
        side.Controls.Add(LabelText("LAUNCHER", 10, true, Theme.Muted), 0, 1);
        var sectionTitle = LabelText("WORKSPACE", 8, true, Theme.Muted); sectionTitle.Margin = new Padding(0, 35, 0, 14); side.Controls.Add(sectionTitle, 0, 2);
        var nav = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
        foreach (var name in new[] { "Overview", "Connection", "Files", "Activity" })
        {
            var button = Theme.Button(name); button.Width = 168; button.Height = 44; button.AutoSize = false; button.Margin = new Padding(0, 0, 0, 7);
            if (button is RoundedButton rounded) rounded.Selected = name == "Overview";
            navigation.Add((button, name));
            button.Click += (_, _) => Navigate(name);
            nav.Controls.Add(button);
        }
        side.Controls.Add(nav, 0, 3);
        var source = Link("Source code", RepositoryAddress.SourceUrl); source.Margin = new Padding(0, 26, 0, 6); side.Controls.Add(source, 0, 4);
        side.Controls.Add(Link("Setup guide", RepositoryAddress.SourceUrl + "/blob/main/docs/SETUP.md"), 0, 5);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
        foreach (var button in new[] { updates, settings }) { button.Width = 168; button.Height = 40; button.AutoSize = false; button.Margin = new Padding(0, 0, 0, 8); footer.Controls.Add(button); }
        footer.Controls.Add(LabelText("made by hn24987", 9, false, Theme.Muted));
        footer.Controls.Add(LabelText("v" + AppVersion.Current, 8, false, Theme.Purple));
        side.Controls.Add(footer, 0, 7);
        shell.Controls.Add(scroll, 1, 0);
        var content = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 5, Padding = new Padding(28, 24, 28, 24), BackColor = Theme.Bg };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 86));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 515));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 226));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scroll.Controls.Add(content);
        var hero = Stack();
        Item(hero, LabelText("Your remote workspace.", 25, true));
        Item(hero, LabelText("Set up once. Launch a Windows session when you need it.", 10, false, Theme.Muted));
        content.Controls.Add(hero, 0, 0);
        var dashboard = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Margin = Padding.Empty };
        dashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        dashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(dashboard, 0, 1);
        setupCard = Theme.Card(); setupCard.Dock = DockStyle.Fill; setupCard.Margin = new Padding(0, 0, 18, 0);
        dashboard.Controls.Add(setupCard, 0, 0);
        var setup = Stack(); setupCard.Controls.Add(setup);
        Item(setup, LabelText("Connection setup", 15, true), 34);
        Item(setup, LabelText("Repository URL", 9, true, Theme.Muted), 22);
        repository.PlaceholderText = "https://github.com/your-name/RDP-Launcher";
        Item(setup, Input(repository), 56);
        forkStatus.Font = new Font("Segoe UI", 9); forkStatus.ForeColor = Theme.Muted; forkStatus.AutoSize = true; forkStatus.Margin = new Padding(0, 0, 0, 8);
        Item(setup, forkStatus, 49);
        tokenStatus.ForeColor = Theme.Purple; tokenStatus.Font = new Font("Segoe UI", 9.5f); tokenStatus.Margin = new Padding(0, 6, 0, 0);
        Item(setup, tokenStatus, 25);
        Item(setup, Row(changeToken, forgetToken), 43);
        Item(setup, LabelText("Session password (optional)", 9, true, Theme.Muted), 22);
        Item(setup, Input(sessionPassword), 50);
        var timeRow = Row(LabelText("Session (minutes)", 9, true, Theme.Muted), duration, LabelText("10–300 minutes", 8.5f, false, Theme.Muted));
        duration.Height = 32; timeRow.Padding = new Padding(0, 7, 0, 4); Item(setup, timeRow, 44);
        var controls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54)); controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        controls.Controls.Add(start, 0, 0); controls.Controls.Add(check, 1, 0); start.Dock = check.Dock = DockStyle.Fill;
        start.Margin = new Padding(0, 4, 8, 4); check.Margin = new Padding(0, 4, 0, 4);
        Item(setup, controls, 56);
        Item(setup, Row(Link("Secrets", () => RepoUrl("settings/secrets/actions")), Link("Auth Keys", "https://login.tailscale.com/admin/settings/keys"), Link("Install Tailscale", "https://tailscale.com/download/windows")), 34);
        Item(setup, Row(Link("Open Tailscale", () => TailscaleCheck.FindExecutable("tailscale-ipn.exe") ?? throw new InvalidOperationException(L.T("Tailscale UI not found. Install it first, or open Tailscale from the Windows Start menu."))), Link("Fork project", RepositoryAddress.SourceUrl + "/fork")), 34);
        Spacer(setup);
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent, Margin = Padding.Empty };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 62)); right.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        dashboard.Controls.Add(right, 1, 0);
        sessionCard = Theme.Card(); sessionCard.Dock = DockStyle.Fill; sessionCard.Margin = new Padding(0, 0, 0, 16); right.Controls.Add(sessionCard, 0, 0);
        var session = Stack(); sessionCard.Controls.Add(session);
        Item(session, Row(LabelText("Your session", 14, true), sessionBadge), 32);
        Item(session, remaining, 51);
        Item(session, address, 35);
        connection.Font = new Font("Segoe UI", 9); connection.ForeColor = Theme.Muted; Item(session, connection, 51);
        var connectRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        connectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70)); connectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        connectRow.Controls.Add(connect, 0, 0); connectRow.Controls.Add(copy, 1, 0); connect.Dock = copy.Dock = DockStyle.Fill;
        connect.Margin = new Padding(0, 2, 8, 2); copy.Margin = new Padding(0, 2, 0, 2); Item(session, connectRow, 45);
        Spacer(session);
        transferCard = Theme.Card(); transferCard.Dock = DockStyle.Fill; transferCard.Margin = Padding.Empty; right.Controls.Add(transferCard, 0, 1);
        var transfer = Stack(); transferCard.Controls.Add(transfer);
        Item(transfer, LabelText("Bring your files home.", 14, true), 30);
        Item(transfer, LabelText("Send selected files from RDP through Taildrop, then receive them here.", 9, false, Theme.Muted), 48);
        receive.Dock = DockStyle.Fill; receive.Margin = Padding.Empty; Item(transfer, receive, 40);
        Spacer(transfer);
        activityCard = Theme.Card(); activityCard.Dock = DockStyle.Fill; activityCard.Margin = Padding.Empty;
        var activity = Stack(); activityCard.Controls.Add(activity); content.Controls.Add(activityCard, 0, 3);
        Item(activity, LabelText("Activity log", 14, true), 30);
        runId.Width = 135; runId.BorderStyle = BorderStyle.FixedSingle;
        Item(activity, Row(LabelText("Run ID", 9, false, Theme.Muted), runId, resume, stopWatch, cancel, Link("View on GitHub", () => RepoUrl("actions"))), 48);
        log.BackColor = Theme.Bg; log.ForeColor = Theme.Muted; log.BorderStyle = BorderStyle.None; log.Font = new Font("Consolas", 9);
        Item(activity, log);
        activity.RowStyles[^1] = new RowStyle(SizeType.Percent, 100);
        var note = LabelText("Closing the launcher keeps your RDP session running. Use Stop workflow to end it.", 8.5f, false, Theme.Muted);
        note.Margin = new Padding(2, 12, 0, 0); content.Controls.Add(note, 0, 4);
        Resize += (_, _) => { forkStatus.MaximumSize = new Size(Math.Max(240, setupCard.ClientSize.Width - 40), 0); connection.MaximumSize = new Size(Math.Max(200, sessionCard.ClientSize.Width - 40), 0); };
    }
    void Navigate(string section)
    {
        foreach (var item in navigation) if (item.Button is RoundedButton button) button.Selected = item.Section == section;
        var target = section switch { "Connection" => setupCard, "Files" => transferCard, "Activity" => activityCard, _ => setupCard };
        scroll.ScrollControlIntoView(target);
        if (section == "Connection") repository.Focus();
        if (section == "Activity") log.Focus();
    }
    static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Padding = Padding.Empty };
        row.BackColor = Color.Transparent;
        row.Controls.AddRange(controls);
        return row;
    }
    LinkLabel Link(string title, string url) => Link(title, () => url);
    LinkLabel Link(string title, Func<string> url)
    {
        var link = new LinkLabel { Text = title, AutoSize = true, Margin = new Padding(0, 8, 14, 4),
            LinkColor = Theme.Purple, ActiveLinkColor = Theme.Accent, VisitedLinkColor = Theme.Purple,
            LinkBehavior = LinkBehavior.HoverUnderline, Font = new Font("Segoe UI", 9) };
        link.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(url()) { UseShellExecute = true }); }
            catch (Exception ex) { Write(ex.Message); }
        };
        return link;
    }
    bool ValidFork()
    {
        try { RepositoryAddress.Parse(repository.Text); return true; }
        catch (InvalidOperationException) { return false; }
    }
    void UpdateStart()
    {
        var valid = false;
        try { var name = RepositoryAddress.Parse(repository.Text); Caption(forkStatus, "Target: {0} · write access and project membership will be verified with GitHub.", name); valid = true; }
        catch (InvalidOperationException ex) { Caption(forkStatus, string.IsNullOrWhiteSpace(repository.Text) ? "Paste the source repository URL or your fork URL to enable Check setup and Get RDP." : ex.Message); }
        start.Enabled = check.Enabled = !busy && valid && !string.IsNullOrWhiteSpace(token);
    }
    void AskForToken()
    {
        using var dialog = new TokenDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            token = dialog.SavedToken;
            Caption(tokenStatus, "Signed in as {0} · token saved", dialog.Login);
            Write(L.T("GitHub token validated and encrypted beside the EXE. Repository permissions are checked during preflight."));
        }
        UpdateStart();
    }
    string RepoUrl(string suffix) => $"https://github.com/{RepositoryAddress.Parse(repository.Text)}/{suffix}";
    GitHubClient NewClient()
    {
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException(L.T("Click Add / change token to connect your GitHub account first."));
        SelectFork();
        return new GitHubClient(RepositoryAddress.Parse(repository.Text), token);
    }
    async Task Execute(Func<GitHubClient, CancellationToken, Task> action)
    {
        if (busy) return;
        busy = true;
        check.Enabled = start.Enabled = resume.Enabled = repository.Enabled = changeToken.Enabled = forgetToken.Enabled = duration.Enabled = sessionPassword.Enabled = runId.Enabled = false;
        stopWatch.Enabled = true;
        using var cts = new CancellationTokenSource();
        operation = cts;
        try { using var api = NewClient(); await action(api, cts.Token); }
        catch (OperationCanceledException) { Write(L.T(cts.IsCancellationRequested ? "Monitoring stopped. The workflow keeps running unless you stop it." : "Network request timed out. Use Resume run to avoid creating duplicate sessions.")); }
        catch (Exception ex) { Write(L.T("Error: ") + ex.Message); }
        finally
        {
            operation = null;
            busy = false;
            if (!IsDisposed)
            {
                check.Enabled = start.Enabled = resume.Enabled = repository.Enabled = changeToken.Enabled = forgetToken.Enabled = duration.Enabled = sessionPassword.Enabled = runId.Enabled = true;
                stopWatch.Enabled = false;
                UpdateStart();
            }
        }
    }
    async Task<string> Preflight(GitHubClient api, CancellationToken ct)
    {
        Write(L.T("Checking local Tailscale and your repository in parallel…"));
        var local = CheckTailscale(ct);
        var github = api.Preflight(Write, ct);
        try { await Task.WhenAll(local, github); }
        catch when (!ct.IsCancellationRequested)
        {
            foreach (var task in new Task[] { local, github })
                if (task.Exception is not null)
                    foreach (var error in task.Exception.Flatten().InnerExceptions) Write(L.T("FAIL · ") + error.Message);
            throw new InvalidOperationException(L.T("Preflight failed. Fix the checks above, then try again. No workflow was started."));
        }
        Save();
        return await github;
    }
    Task CheckTailscale(CancellationToken ct) => TailscaleCheck.Check(Write, ct);
    async Task<long> Discover(GitHubClient api, CancellationToken ct)
    {
        var until = DateTimeOffset.UtcNow.AddMinutes(3);
        while (DateTimeOffset.UtcNow < until)
        {
            var found = await api.FindRun(pendingRequest!, ct);
            if (found is long id) { runId.Text = id.ToString(); Save(); Write(L.F("Run ID: {0}", id)); return id; }
            await Task.Delay(5000, ct);
        }
        throw new InvalidOperationException(L.T("Run not found yet. Open Actions / logs or use Resume run to search again; do not dispatch again."));
    }
    async Task Monitor(GitHubClient api, long id, CancellationToken ct)
    {
        // Keep known expiry notifications if refreshing this run fails or is cancelled.
        if (countdown.RunId != id) ClearConnection();
        runId.Text = id.ToString(); Save();
        var until = DateTimeOffset.UtcNow.AddMinutes(25);
        string? previous = null;
        while (DateTimeOffset.UtcNow < until)
        {
            var run = await api.Get($"actions/runs/{id}", ct);
            if (run.GetProperty("path").GetString() != ".github/workflows/main.yml")
                throw new InvalidOperationException(L.T("This run does not belong to main.yml."));
            var status = run.GetProperty("status").GetString();
            if (status != previous) { Write(L.F("Workflow: {0}", status)); previous = status; }
            if (status == "completed")
            {
                ClearConnection();
                var conclusion = run.GetProperty("conclusion").GetString();
                throw new InvalidOperationException(L.F("Workflow ended: {0}. See Actions / logs for details. Clear Run ID to start a new session.", conclusion));
            }
            var info = await api.ReadConnection(id, ct);
            if (info is not null)
            {
                // Recheck after artifact retrieval so a finished session isn't displayed as ready.
                var latest = await api.Get($"actions/runs/{id}", ct);
                if (latest.GetProperty("status").GetString() == "completed") continue;
                if (customPasswordPending && pendingRequest is not null && run.GetProperty("display_title").GetString() == "rdp-app-" + pendingRequest)
                {
                    try { await api.DeletePassword(pendingRequest, ct); customPasswordPending = false; Save(); } catch (Exception) { Write(L.T("The session is ready. Its temporary password secret may need manual removal in GitHub Settings.")); }
                }
                currentIp = info.Ip;
                address.Text = info.Ip;
                captions.Remove(address);
                Caption(sessionBadge, "Session ready");
                sessionBadge.ForeColor = Theme.Accent;
                Caption(connection, "Username: .\\{0}\nExpected end: {1}", info.Username, info.ExpiresAt.ToLocalTime().ToString("g"));
                countdown.Configure(id, info.ExpiresAt, DateTimeOffset.UtcNow);
                TickCountdown();
                copy.Enabled = connect.Enabled = true;
                Write(L.T("PASS · RDP is ready. Use the password selected in this app, or 123456 if left blank."));
                using var tcp = new TcpClient();
                try { await tcp.ConnectAsync(info.Ip, 3389, ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), ct); Write(L.T("PASS · Your device can reach TCP 3389. Click Open Remote Desktop.")); }
                catch (Exception ex) when (ex is SocketException or TimeoutException) { Write(L.T("TCP 3389 is not reachable yet. Check the tailnet, device approval and ACLs/grants for port 3389. The runner IP is available; retry after fixing access.")); }
                Write(L.T("Connection details reflect the last check. Use Resume run to refresh; Stop workflow to end the session."));
                return;
            }
            await Task.Delay(6000, ct);
        }
        throw new InvalidOperationException(L.T("Waited 25 minutes. Check the queue, quota, billing or approvals in Actions, then use Resume run."));
    }
    void SelectFork()
    {
        var target = RepositoryAddress.Parse(repository.Text);
        if (string.Equals(target, sessionRepository, StringComparison.OrdinalIgnoreCase)) return;
        if (sessionRepository is not null)
            sessions[sessionRepository] = new SavedSession("https://github.com/" + sessionRepository, runId.Text.Trim(), pendingRequest, customPasswordPending);
        runId.Text = "";
        pendingRequest = null;
        customPasswordPending = false;
        if (sessions.TryGetValue(target, out var saved)) { runId.Text = saved.RunId; pendingRequest = saved.RequestId; customPasswordPending = saved.CustomPasswordPending; }
        sessionRepository = target;
        ClearConnection();
    }
    void Save()
    {
        var target = RepositoryAddress.Parse(repository.Text);
        sessions[target] = new SavedSession(repository.Text.Trim(), runId.Text.Trim(), pendingRequest, customPasswordPending);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(new SavedState(repository.Text.Trim(), sessions)));
    }
    void ClearConnection()
    {
        currentIp = null;
        countdown.Clear();
        remaining.Text = "--:--:--";
        Caption(address, "Not connected");
        Caption(sessionBadge, "No active session");
        sessionBadge.ForeColor = Theme.Muted;
        Caption(connection, "No RDP session connected.");
        copy.Enabled = connect.Enabled = false;
    }
    void RegisterCaptions(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if ((control is Label or Button or LinkLabel) && !string.IsNullOrEmpty(control.Text))
                captions[control] = (control.Text, Array.Empty<object>());
            RegisterCaptions(control);
        }
    }
    void Caption(Control control, string key, params object[] args)
    {
        captions[control] = (key, args);
        control.Text = args.Length == 0 ? L.T(key) : L.F(key, args);
    }
    void ApplyLanguage()
    {
        Text = L.F("RDP Launcher v{0} · Created by hn24987", AppVersion.Current);
        sessionPassword.PlaceholderText = L.T("Optional — default: 123456");
        sessionPassword.AccessibleName = L.T("Session password (optional)");
        foreach (var item in captions)
            item.Key.Text = item.Value.Args.Length == 0 ? L.T(item.Value.Key) : L.F(item.Value.Key, item.Value.Args);
        UpdateStart();
        TickCountdown();
    }
    void TickCountdown()
    {
        if (IsDisposed || warningOpen) return;
        var now = DateTimeOffset.UtcNow;
        var left = countdown.Remaining(now);
        remaining.Text = left.HasValue ? left.Value.ToString(@"hh\:mm\:ss") : "--:--:--";
        foreach (var warning in countdown.Tick(now))
        {
            if (warning == SessionWarning.Expired)
            {
                currentIp = null;
                Caption(address, "Session expired");
                Caption(sessionBadge, "Session expired");
                sessionBadge.ForeColor = Theme.Danger;
                copy.Enabled = connect.Enabled = false;
                Caption(connection, "Session expired. The remote machine may have stopped. Save files before future sessions end.");
                Write(L.T("Session expired"));
                continue;
            }
            warningOpen = true;
            try
            {
                SystemSounds.Exclamation.Play();
                MessageBox.Show(this, L.F("Your RDP session has about {0} minutes left. Save your files now. Receive files via Taildrop before the session ends.",
                    Math.Max(1, (int)Math.Ceiling((countdown.Remaining(DateTimeOffset.UtcNow) ?? TimeSpan.Zero).TotalMinutes))), L.T("Session ending soon"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { warningOpen = false; }
        }
    }
    async Task CheckUpdates()
    {
        updates.Enabled = false;
        try
        {
            Write(L.T("Checking for updates…"));
            using var service = new UpdateService();
            var latest = await service.CheckAsync(AppVersion.Current, lifetime.Token);
            if (IsDisposed) return;
            if (latest is null) { MessageBox.Show(this, L.T("No stable release is available yet.")); return; }
            if (!latest.IsNewer) { MessageBox.Show(this, L.F("You are up to date ({0}).", AppVersion.Current)); return; }
            if (MessageBox.Show(this, L.F("Version {0} is available. You are using {1}. Open the release page?", latest.Version, AppVersion.Current),
                L.T("Update available"), MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(latest.Url) { UseShellExecute = true });
        }
        catch (OperationCanceledException) { if (!IsDisposed) Write(L.T("Update check cancelled or timed out.")); }
        catch (Exception ex) { Write(ex.Message); }
        finally { if (!IsDisposed) updates.Enabled = true; }
    }
    async Task ReceiveFiles()
    {
        if (receiving) return;
        MessageBox.Show(this, L.T("Send selected remote files through Taildrop first. This receives this PC's inbox; it does not browse the remote filesystem."), L.T("File transfer"));
        using var folder = new FolderBrowserDialog { Description = L.T("Choose a folder for files received from your RDP machine."), UseDescriptionForTitle = true };
        if (folder.ShowDialog(this) != DialogResult.OK) return;
        receiving = true;
        receive.Enabled = false;
        try
        {
            Write(L.T("Receiving files"));
            var result = await TaildropService.ReceiveAsync(folder.SelectedPath, lifetime.Token);
            if (IsDisposed) return;
            if (!result.Success)
            {
                Write(L.T("Tailscale could not receive files. Check that both devices are online, owned by the same user, and allowed to use Taildrop."));
                if (result.Error.Length > 0) Write(result.Error);
                return;
            }
            Write(L.F("Receive command completed for {0}.", folder.SelectedPath));
            MessageBox.Show(this, L.T("Receive request completed. Check the selected folder and Downloads if Tailscale already saved incoming files. An empty inbox does not mean files were backed up."), L.T("File transfer"));
        }
        catch (OperationCanceledException) { if (!IsDisposed) Write(L.T("File retrieval cancelled.")); }
        catch (Exception ex) { Write(ex.Message); }
        finally { receiving = false; if (!IsDisposed) receive.Enabled = true; }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !resourcesDisposed)
        {
            resourcesDisposed = true;
            lifetime.Cancel();
            operation?.Cancel();
            countdownTimer.Stop();
            countdownTimer.Dispose();
            lifetime.Dispose();
        }
        base.Dispose(disposing);
    }
    void Write(string message)
    {
        if (!IsDisposed) log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
