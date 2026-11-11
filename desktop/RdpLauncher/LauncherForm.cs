using System.Diagnostics;
using System.Net.Sockets;
using System.Media;
using System.Text.Json;

namespace RdpLauncher;

public sealed class LauncherForm : Form
{
    readonly TextBox repository = new() { PlaceholderText = "https://github.com/YOUR-USERNAME/RDP", Width = 480 };
    readonly Button updates = new() { Text = "Check for updates", AutoSize = true };
    readonly Button settings = new() { Text = "Settings", AutoSize = true };
    readonly Button receive = new() { Text = "Receive files", AutoSize = true };
    readonly Label remaining = new() { AutoSize = true };
    readonly System.Windows.Forms.Timer countdownTimer = new() { Interval = 1000 };
    readonly SessionCountdown countdown = new();
    readonly CancellationTokenSource lifetime = new();
    readonly Dictionary<Control, (string Key, object[] Args)> captions = new();
    bool receiving;
    bool warningOpen;
    bool resourcesDisposed;
    string token = "";
    readonly Label forkStatus = new() { AutoSize = true, MaximumSize = new Size(880, 0) };
    readonly Label tokenStatus = new() { Text = "GitHub token not configured", AutoSize = true };
    readonly Button changeToken = new() { Text = "Add / change token", AutoSize = true };
    readonly Button forgetToken = new() { Text = "Forget token", AutoSize = true };
    readonly NumericUpDown duration = new() { Minimum = 10, Maximum = 300, Value = 60, Width = 85 };
    readonly TextBox runId = new() { Width = 180 };
    readonly Button check = new() { Text = "Check setup", AutoSize = true };
    readonly Button start = new() { Text = "Get RDP", AutoSize = true };
    readonly Button resume = new() { Text = "Resume run", AutoSize = true };
    readonly Button stopWatch = new() { Text = "Stop monitoring", AutoSize = true, Enabled = false };
    readonly Button cancel = new() { Text = "Stop workflow", AutoSize = true };
    readonly Button connect = new() { Text = "Open Remote Desktop", AutoSize = true, Enabled = false };
    readonly Button copy = new() { Text = "Copy IP", AutoSize = true, Enabled = false };
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    readonly Label connection = new() { Text = "No RDP session connected.", AutoSize = true, MaximumSize = new Size(830, 0) };
    readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RdpLauncher", "session-v2.json");
    string? pendingRequest;
    string? sessionRepository;
    string? currentIp;
    CancellationTokenSource? operation;
    bool busy;
    sealed record SavedSession(string Repository, string RunId, string? RequestId);
    sealed record SavedState(string Repository, Dictionary<string, SavedSession> Sessions);
    Dictionary<string, SavedSession> sessions = new(StringComparer.OrdinalIgnoreCase);

    public LauncherForm()
    {
        Text = "RDP Launcher v1.1.1 · Created by hn24987";
        Size = new Size(1020, 900);
        MinimumSize = new Size(860, 720);
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 14, Padding = new Padding(18) };
        for (var row = 0; row < 13; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "RDP Launcher", Font = new Font("Segoe UI", 21, FontStyle.Bold), AutoSize = true });
        layout.Controls.Add(Row(new Label { Text = "Created by hn24987", AutoSize = true },
            Link("Source: github.com/hn24987/RDP-Launcher", RepositoryAddress.SourceUrl), Link("Fork this project", RepositoryAddress.SourceUrl + "/fork")));
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(880, 0), Text = "Use a repository you control. Owners can use hn24987/RDP-Launcher directly; other users should fork the project. Enable Actions and add the required secrets." });
        layout.Controls.Add(Row(new Label { Text = "Repository URL", AutoSize = true }, repository));
        layout.Controls.Add(forkStatus);
        layout.Controls.Add(Row(tokenStatus, changeToken, forgetToken,
            new Label { Text = "Session (minutes)", AutoSize = true }, duration));
        layout.Controls.Add(Row(check, start, resume, stopWatch, cancel));
        layout.Controls.Add(Row(new Label { Text = "Run ID", AutoSize = true }, runId,
            Link("Secrets", () => RepoUrl("settings/secrets/actions")), Link("Actions / logs", () => RepoUrl("actions")),
            Link("Auth Keys", "https://login.tailscale.com/admin/settings/keys"), Link("Install Tailscale", "https://tailscale.com/download/windows"),
            Link("Open Tailscale", () => TailscaleCheck.FindExecutable("tailscale-ipn.exe") ?? throw new InvalidOperationException(L.T("Tailscale UI not found. Install it first, or open Tailscale from the Windows Start menu.")))));
        layout.Controls.Add(Row(updates, settings, receive));
        layout.Controls.Add(remaining);
        layout.Controls.Add(connection);
        layout.Controls.Add(Row(copy, connect));
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(880, 0), Padding = new Padding(0, 5, 0, 8), Text = "Get RDP runs preflight automatically. When ready, open Remote Desktop and sign in as .\\RDP with your RDP_PASSWORD secret. Match any certificate prompt to the displayed IP. Closing this app does not stop your workflow. Your token is encrypted and saved beside the EXE." });
        layout.Controls.Add(log);
        Controls.Add(layout);
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
                pendingRequest = Guid.NewGuid().ToString("N");
                Save(); // Persist correlation BEFORE dispatch: a lost response must not cause duplicate dispatch.
                Write(L.F("Dispatching session {0}…", pendingRequest));
                await api.Dispatch(branch, pendingRequest, (int)duration.Value, ct);
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
        Write(L.T("First run: select the source repository (with write access) or your fork. Enable Actions and add TAILSCALE_AUTH_KEY and RDP_PASSWORD. Forks do not inherit secrets."));
        UpdateStart();
    }
    static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 5, 0, 5) };
        row.Controls.AddRange(controls);
        return row;
    }
    LinkLabel Link(string title, string url) => Link(title, () => url);
    LinkLabel Link(string title, Func<string> url)
    {
        var link = new LinkLabel { Text = title, AutoSize = true, Margin = new Padding(8) };
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
        check.Enabled = start.Enabled = resume.Enabled = repository.Enabled = changeToken.Enabled = forgetToken.Enabled = duration.Enabled = runId.Enabled = false;
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
                check.Enabled = start.Enabled = resume.Enabled = repository.Enabled = changeToken.Enabled = forgetToken.Enabled = duration.Enabled = runId.Enabled = true;
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
                currentIp = info.Ip;
                Caption(connection, "IP: {0}   ·   User: .\\{1}\nExpected end: {2} (your local time)", info.Ip, info.Username, info.ExpiresAt.ToLocalTime().ToString("g"));
                countdown.Configure(id, info.ExpiresAt, DateTimeOffset.UtcNow);
                TickCountdown();
                copy.Enabled = connect.Enabled = true;
                Write(L.T("PASS · The runner has enabled RDP. Use the password you set in the RDP_PASSWORD secret."));
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
            sessions[sessionRepository] = new SavedSession("https://github.com/" + sessionRepository, runId.Text.Trim(), pendingRequest);
        runId.Text = "";
        pendingRequest = null;
        if (sessions.TryGetValue(target, out var saved)) { runId.Text = saved.RunId; pendingRequest = saved.RequestId; }
        sessionRepository = target;
        ClearConnection();
    }
    void Save()
    {
        var target = RepositoryAddress.Parse(repository.Text);
        sessions[target] = new SavedSession(repository.Text.Trim(), runId.Text.Trim(), pendingRequest);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, JsonSerializer.Serialize(new SavedState(repository.Text.Trim(), sessions)));
    }
    void ClearConnection()
    {
        currentIp = null;
        countdown.Clear();
        remaining.Text = "";
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
        remaining.Text = left.HasValue ? L.F("Remaining: {0}", left.Value.ToString(@"hh\:mm\:ss")) : "";
        foreach (var warning in countdown.Tick(now))
        {
            if (warning == SessionWarning.Expired)
            {
                currentIp = null;
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
