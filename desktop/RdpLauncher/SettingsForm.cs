namespace RdpLauncher;

public sealed class SettingsForm : Form
{
    public SettingsForm()
    {
        Text = L.T("Settings");
        ClientSize = new Size(510, 195);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        Font = new Font("Segoe UI", 10);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.Add(new Label { AutoSize = true, Text = L.T("Language") });
        var language = new ComboBox { Width = 245, DropDownStyle = ComboBoxStyle.DropDownList };
        language.Items.AddRange(new object[] { "English", "Tiếng Việt" });
        language.SelectedIndex = L.Language == "vi" ? 1 : 0;
        layout.Controls.Add(language);
        layout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(450, 0), Text = L.T("Language changes apply immediately. Existing log entries keep their original language.") });
        var save = new Button { AutoSize = true, Text = L.T("Save") };
        save.Click += (_, _) =>
        {
            try { L.SetLanguage(language.SelectedIndex == 1 ? "vi" : "en"); DialogResult = DialogResult.OK; Close(); }
            catch (Exception error) { MessageBox.Show(this, L.T("Could not save language preference: ") + error.Message, L.T("Settings")); }
        };
        layout.Controls.Add(save);
        Controls.Add(layout);
        AcceptButton = save;
    }
}
