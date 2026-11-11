using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;

namespace RdpLauncher;

public sealed class LauncherForm : Form
{
    readonly TextBox repository = new() { PlaceholderText = "https://github.com/YOUR-USERNAME/RDP", Width = 480 };
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
        Size = new Size(960, 830);
        MinimumSize = new Size(860, 720);
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 12, Padding = new Padding(18) };
        for (var row = 0; row < 11; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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
            Link("Open Tailscale", () => TailscaleCheck.FindExecutable("tailscale-ipn.exe") ?? throw new InvalidOperationException("Tailscale UI not found. Install it first, or open Tailscale from the Windows Start menu."))));
        layout.Controls.Add(connection);
        layout.Controls.Add(Row(copy, connect));
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(880, 0), Padding = new Padding(0, 5, 0, 8), Text = "Get RDP runs preflight automatically. When ready, open Remote Desktop and sign in as .\\RDP with your RDP_PASSWORD secret. Match any certificate prompt to the displayed IP. Closing this app does not stop your workflow. Your token is encrypted and saved beside the EXE." });
        layout.Controls.Add(log);
        Controls.Add(layout);
        changeToken.Click += (_, _) => AskForToken();
        forgetToken.Click += (_, _) =>
        {
            if (MessageBox.Show("Delete the saved token from this folder?", "Forget token", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { TokenStore.Delete(); token = ""; tokenStatus.Text = "GitHub token not configured"; UpdateStart(); }
            catch (Exception ex) { Write("Could not delete token file: " + ex.Message); }
        };
        repository.TextChanged += (_, _) => UpdateStart();
        repository.Validated += (_, _) => { if (ValidFork()) { try { SelectFork(); Save(); } catch (Exception ex) { Write(ex.Message); } } };
        Shown += async (_, _) =>
        {
            try
            {
                if (TokenStore.Exists) { token = TokenStore.Load(); tokenStatus.Text = "Saved GitHub token loaded"; }
            }
            catch { Write("The saved token cannot be decrypted. Enter it again for this Windows account."); }
            if (string.IsNullOrWhiteSpace(token)) AskForToken();
            UpdateStart();
            if (!string.IsNullOrWhiteSpace(token) && ValidFork())
                await Execute(async (api, ct) => { await Preflight(api, ct); Write("Preflight complete. Click Get RDP when ready."); });
            else repository.Focus();
        };
        check.Click += async (_, _) => await Execute(async (api, ct) => { await Preflight(api, ct); Write("Preflight complete. No workflow has been started."); });
        start.Click += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(runId.Text) || pendingRequest is not null)
            {
                MessageBox.Show("A previous session is saved. Use Resume run first. After it ends, clear Run ID before starting a new session.");
                if (string.IsNullOrWhiteSpace(runId.Text) && MessageBox.Show("The previous request may already have created a machine. Have you checked Actions and still want to discard the saved request ID?", "Discard saved request", MessageBoxButtons.YesNo) == DialogResult.Yes) { pendingRequest = null; Save(); }
                return;
            }
            await Execute(async (api, ct) =>
            {
                var branch = await Preflight(api, ct);
                sessionRepository = RepositoryAddress.Parse(repository.Text);
                pendingRequest = Guid.NewGuid().ToString("N");
                Save(); // Persist correlation BEFORE dispatch: a lost response must not cause duplicate dispatch.
                Write($"Dispatching session {pendingRequest}…");
                await api.Dispatch(branch, pendingRequest, (int)duration.Value, ct);
                Write("GitHub accepted the request. Finding your run…");
                var id = await Discover(api, ct);
                await Monitor(api, id, ct);
            });
        };
        resume.Click += async (_, _) => await Execute(async (api, ct) =>
        {
            if (pendingRequest is not null && string.IsNullOrWhiteSpace(runId.Text) && sessionRepository != RepositoryAddress.Parse(repository.Text))
                throw new InvalidOperationException("This pending request belongs to a different fork. Restore its fork URL before resuming.");
            await CheckTailscale(ct);
            long id;
            if (string.IsNullOrWhiteSpace(runId.Text) && pendingRequest is not null) id = await Discover(api, ct);
            else if (!long.TryParse(runId.Text, out id) || id <= 0) throw new InvalidOperationException("Enter the Run ID from your GitHub Actions URL: /actions/runs/123456.");
            await Monitor(api, id, ct);
        });
        stopWatch.Click += (_, _) => operation?.Cancel();
        cancel.Click += async (_, _) =>
        {
            if (!long.TryParse(runId.Text, out var id) || id <= 0) { MessageBox.Show("Enter a Run ID to stop."); return; }
            if (MessageBox.Show($"Stop workflow {id}? This will disconnect the RDP session.", "Stop session", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try
            {
                using var api = NewClient();
                await api.Cancel(id, CancellationToken.None);
                operation?.Cancel();
                ClearConnection();
                Write("Stop requested. Check the final status in Actions / logs.");
            }
            catch (Exception ex) { Write(ex.Message); }
        };
        copy.Click += (_, _) => { if (currentIp is not null) Clipboard.SetText(currentIp); };
        connect.Click += (_, _) =>
        {
            try { if (currentIp is not null) Process.Start(new ProcessStartInfo("mstsc.exe") { UseShellExecute = true, ArgumentList = { $"/v:{currentIp}" } }); }
            catch (Exception ex) { Write("Could not open Remote Desktop: " + ex.Message); }
        };
        FormClosing += (_, e) =>
        {
            if (busy && MessageBox.Show("Closing the app stops monitoring only. The workflow keeps running; you can resume monitoring later. Close the app?", "Close", MessageBoxButtons.YesNo) != DialogResult.Yes)
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
        catch { Write("Could not load the previous session. You can enter a Run ID manually."); }
        Write("First run: select the source repository (with write access) or your fork. Enable Actions and add TAILSCALE_AUTH_KEY and RDP_PASSWORD. Forks do not inherit secrets.");
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
        try { var name = RepositoryAddress.Parse(repository.Text); forkStatus.Text = $"Target: {name} · write access and project membership will be verified with GitHub."; valid = true; }
        catch (InvalidOperationException ex) { forkStatus.Text = string.IsNullOrWhiteSpace(repository.Text) ? "Paste the source repository URL or your fork URL to enable Check setup and Get RDP." : ex.Message; }
        start.Enabled = check.Enabled = !busy && valid && !string.IsNullOrWhiteSpace(token);
    }
    void AskForToken()
    {
        using var dialog = new TokenDialog();
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            token = dialog.SavedToken;
            tokenStatus.Text = $"Signed in as {dialog.Login} · token saved";
            Write("GitHub token validated and encrypted beside the EXE. Repository permissions are checked during preflight.");
        }
        UpdateStart();
    }
    string RepoUrl(string suffix) => $"https://github.com/{RepositoryAddress.Parse(repository.Text)}/{suffix}";
    GitHubClient NewClient()
    {
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Click Add / change token to connect your GitHub account first.");
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
        catch (OperationCanceledException) { Write(cts.IsCancellationRequested ? "Monitoring stopped. The workflow keeps running unless you stop it." : "Network request timed out. Use Resume run to avoid creating duplicate sessions."); }
        catch (Exception ex) { Write("Error: " + ex.Message); }
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
        ClearConnection();
        Write("Checking local Tailscale and your repository in parallel…");
        var local = CheckTailscale(ct);
        var github = api.Preflight(Write, ct);
        try { await Task.WhenAll(local, github); }
        catch when (!ct.IsCancellationRequested)
        {
            foreach (var task in new Task[] { local, github })
                if (task.Exception is not null)
                    foreach (var error in task.Exception.Flatten().InnerExceptions) Write("FAIL · " + error.Message);
            throw new InvalidOperationException("Preflight failed. Fix the checks above, then try again. No workflow was started.");
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
            if (found is long id) { runId.Text = id.ToString(); Save(); Write($"Run ID: {id}"); return id; }
            await Task.Delay(5000, ct);
        }
        throw new InvalidOperationException("Run not found yet. Open Actions / logs or use Resume run to search again; do not dispatch again.");
    }
    async Task Monitor(GitHubClient api, long id, CancellationToken ct)
    {
        ClearConnection();
        runId.Text = id.ToString(); Save();
        var until = DateTimeOffset.UtcNow.AddMinutes(25);
        string? previous = null;
        while (DateTimeOffset.UtcNow < until)
        {
            var run = await api.Get($"actions/runs/{id}", ct);
            if (run.GetProperty("path").GetString() != ".github/workflows/main.yml")
                throw new InvalidOperationException("This run does not belong to main.yml.");
            var status = run.GetProperty("status").GetString();
            if (status != previous) { Write($"Workflow: {status}"); previous = status; }
            if (status == "completed")
            {
                ClearConnection();
                var conclusion = run.GetProperty("conclusion").GetString();
                throw new InvalidOperationException($"Workflow ended: {conclusion}. See Actions / logs for details. Clear Run ID to start a new session.");
            }
            var info = await api.ReadConnection(id, ct);
            if (info is not null)
            {
                // Recheck after artifact retrieval so a finished session isn't displayed as ready.
                var latest = await api.Get($"actions/runs/{id}", ct);
                if (latest.GetProperty("status").GetString() == "completed") continue;
                currentIp = info.Ip;
                connection.Text = $"IP: {info.Ip}   ·   User: .\\{info.Username}\nExpected end: {info.ExpiresAt.ToLocalTime():HH:mm dd/MM/yyyy} (your local time)";
                copy.Enabled = connect.Enabled = true;
                Write("PASS · The runner has enabled RDP. Use the password you set in the RDP_PASSWORD secret.");
                using var tcp = new TcpClient();
                try { await tcp.ConnectAsync(info.Ip, 3389, ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), ct); Write("PASS · Your device can reach TCP 3389. Click Open Remote Desktop."); }
                catch (Exception ex) when (ex is SocketException or TimeoutException) { Write("TCP 3389 is not reachable yet. Check the tailnet, device approval and ACLs/grants for port 3389. The runner IP is available; retry after fixing access."); }
                Write("Connection details reflect the last check. Use Resume run to refresh; Stop workflow to end the session.");
                return;
            }
            await Task.Delay(6000, ct);
        }
        throw new InvalidOperationException("Waited 25 minutes. Check the queue, quota, billing or approvals in Actions, then use Resume run.");
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
    void ClearConnection() { currentIp = null; connection.Text = "No RDP session connected."; copy.Enabled = connect.Enabled = false; }
    void Write(string message)
    {
        if (!IsDisposed) log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
