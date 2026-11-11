namespace RdpLauncher;

public sealed class TokenDialog : Form
{
    readonly TextBox input = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    readonly Label status = new() { AutoSize = true, MaximumSize = new Size(580, 0) };
    readonly Button save = new() { Text = L.T("Validate and save token"), AutoSize = true };
    readonly CancellationTokenSource lifetime = new();
    public string SavedToken { get; private set; } = "";
    public string Login { get; private set; } = "";
    public TokenDialog()
    {
        Text = L.T("GitHub authentication · RDP Launcher");
        ClientSize = new Size(630, 390);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10);
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 7 };
        panel.Controls.Add(new Label { Text = L.T("Connect your GitHub account"), Font = new Font("Segoe UI", 16, FontStyle.Bold), AutoSize = true });
        panel.Controls.Add(new Label { Text = L.T("Create a fine-grained personal access token for the repository you will run.\nOwners can select hn24987/RDP-Launcher directly.\nRepository permissions: Actions — Read and write; Contents, Secrets, Administration — Read-only. Metadata is automatic."), AutoSize = true, MaximumSize = new Size(580, 0) });
        var link = new LinkLabel { Text = L.T("Create a GitHub token"), AutoSize = true };
        link.LinkClicked += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/settings/personal-access-tokens/new") { UseShellExecute = true });
        panel.Controls.Add(link);
        panel.Controls.Add(input);
        panel.Controls.Add(new Label { Text = L.T("The token is encrypted for your Windows account and saved as github-token.dat next to this EXE. Next time, it loads automatically. Keep the app in a writable folder. Moving it to another Windows account requires signing in again."), AutoSize = true, MaximumSize = new Size(580, 0) });
        panel.Controls.Add(save);
        panel.Controls.Add(status);
        Controls.Add(panel);
        AcceptButton = save;
        save.Click += async (_, _) =>
        {
            var value = input.Text.Trim();
            if (value.Length == 0) { status.Text = L.T("Enter your GitHub token."); return; }
            save.Enabled = input.Enabled = false;
            status.Text = L.T("Validating token with GitHub…");
            try
            {
                using var api = new GitHubClient(RepositoryAddress.Source, value);
                Login = await api.GetIdentity(lifetime.Token);
                TokenStore.Save(value);
                SavedToken = value;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (OperationCanceledException) { if (!IsDisposed) status.Text = L.T("GitHub request timed out. Try again."); }
            catch (UnauthorizedAccessException) { status.Text = L.T("Cannot save beside the EXE. Move the app to a writable folder, then try again."); }
            catch (Exception ex) { if (!IsDisposed) status.Text = ex.Message; }
            finally { if (!IsDisposed) save.Enabled = input.Enabled = true; }
        };
        FormClosed += (_, _) => lifetime.Cancel();
    }
}
