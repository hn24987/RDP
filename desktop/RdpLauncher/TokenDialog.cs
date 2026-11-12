namespace RdpLauncher;

public sealed class TokenDialog : Form
{
    readonly TextBox input = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11) };
    readonly Label status = Theme.Label("", 9, color: Theme.Danger);
    readonly Button save = Theme.Button(L.T("Validate and save token"), primary: true);
    readonly CancellationTokenSource lifetime = new();
    bool resourcesDisposed;
    public string SavedToken { get; private set; } = "";
    public string Login { get; private set; } = "";

    public TokenDialog()
    {
        Text = L.T("GitHub authentication · RDP Launcher");
        ClientSize = new Size(620, 620);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(26), ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var hero = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        hero.Controls.Add(Theme.Label(L.T("GITHUB ACCESS"), 8.5f, true, Theme.Accent));
        hero.Controls.Add(Theme.Label(L.T("Connect your GitHub account"), 21, true));
        hero.Controls.Add(Theme.Label(L.T("Your workspace starts with a secure connection."), 9.5f, color: Theme.Muted));
        layout.Controls.Add(hero, 0, 0);

        var card = Theme.Card(); card.Dock = DockStyle.Fill; card.Padding = new Padding(22);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, BackColor = Color.Transparent, Margin = new Padding(0) };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 8; i++) content.RowStyles.Add(new RowStyle(i == 5 ? SizeType.Percent : SizeType.AutoSize, i == 5 ? 100 : 0));
        content.Controls.Add(Theme.Label(L.T("Personal access token"), 12, true));
        var permissions = Theme.Label(L.T("Choose your repository. Permissions: Actions — Read and write; Contents and Administration — Read-only; Secrets — Read-only (Read and write for custom passwords)."), 9.5f, color: Theme.Muted);
        permissions.MaximumSize = new Size(510, 0);
        content.Controls.Add(permissions);
        var link = new LinkLabel { Text = L.T("Create a GitHub token"), AutoSize = true, Margin = new Padding(0, 3, 0, 16) };
        link.LinkClicked += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/settings/personal-access-tokens/new") { UseShellExecute = true }); }
            catch (Exception error) { status.Text = error.Message; }
        };
        content.Controls.Add(link);
        var inputFrame = new Panel { Dock = DockStyle.Fill, Height = 46, BackColor = Theme.Raised, Padding = new Padding(12, 11, 12, 8), Margin = new Padding(0, 0, 0, 15) };
        input.AccessibleName = L.T("Personal access token");
        inputFrame.Controls.Add(input); content.Controls.Add(inputFrame);
        var privacy = Theme.Label(L.T("Encrypted with Windows DPAPI and saved beside the EXE. Your token loads automatically next time. Keep this app in a writable folder."), 9.5f, color: Theme.Muted);
        privacy.MaximumSize = new Size(510, 0);
        content.Controls.Add(privacy);
        content.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent });
        status.MaximumSize = new Size(510, 0); status.MinimumSize = new Size(0, 42); status.Dock = DockStyle.Fill;
        content.Controls.Add(status);
        save.Dock = DockStyle.Fill; save.Height = 46; save.Margin = new Padding(0, 5, 0, 0);
        content.Controls.Add(save);
        card.Controls.Add(content); layout.Controls.Add(card, 0, 1);
        var footer = Theme.Label(L.T("Created by hn24987"), 8.5f, color: Theme.Muted); footer.Margin = new Padding(0, 12, 0, 0);
        layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);
        Theme.Apply(this);
        AcceptButton = save;
        Shown += (_, _) => input.Focus();
        save.Click += async (_, _) =>
        {
            var value = input.Text.Trim();
            if (value.Length == 0) { status.Text = L.T("Enter your GitHub token."); return; }
            save.Enabled = input.Enabled = false;
            status.ForeColor = Theme.Muted;
            status.Text = L.T("Validating token with GitHub…");
            try
            {
                using var api = new GitHubClient(RepositoryAddress.Source, value);
                Login = await api.GetIdentity(lifetime.Token);
                if (IsDisposed) return;
                TokenStore.Save(value);
                SavedToken = value;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (OperationCanceledException) { if (!IsDisposed) status.Text = L.T("GitHub request timed out. Try again."); }
            catch (UnauthorizedAccessException) { if (!IsDisposed) status.Text = L.T("Cannot save beside the EXE. Move the app to a writable folder, then try again."); }
            catch (Exception error) { if (!IsDisposed) status.Text = error.Message; }
            finally { if (!IsDisposed) { status.ForeColor = Theme.Danger; save.Enabled = input.Enabled = true; } }
        };
        FormClosed += (_, _) => { if (!resourcesDisposed) lifetime.Cancel(); };
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !resourcesDisposed)
        {
            resourcesDisposed = true; lifetime.Cancel(); lifetime.Dispose();
        }
        base.Dispose(disposing);
    }
}
