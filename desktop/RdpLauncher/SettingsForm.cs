namespace RdpLauncher;

public sealed class SettingsForm : Form
{
    public SettingsForm()
    {
        Text = L.T("Settings");
        ClientSize = new Size(510, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = MinimizeBox = false;
        Font = new Font("Segoe UI", 10);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(25), ColumnCount = 1, RowCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var hero = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        hero.Controls.Add(Theme.Label(L.T("MAKE IT YOURS"), 8.5f, true, Theme.Accent));
        hero.Controls.Add(Theme.Label(L.T("Settings"), 22, true));
        layout.Controls.Add(hero, 0, 0);
        var card = Theme.Card(); card.Dock = DockStyle.Fill;
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.Transparent };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        content.Controls.Add(Theme.Label(L.T("Language"), 10, true), 0, 0);
        var language = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 0, 0, 12), AccessibleName = L.T("Language") };
        language.Items.AddRange(new object[] { "English", "Tiếng Việt" });
        language.SelectedIndex = L.Language == "vi" ? 1 : 0;
        content.Controls.Add(language, 0, 1);
        var explanation = Theme.Label(L.T("Language changes apply immediately. Existing log entries keep their original language."), 9, color: Theme.Muted);
        explanation.MaximumSize = new Size(405, 0); explanation.Dock = DockStyle.Fill;
        content.Controls.Add(explanation, 0, 2);
        var save = Theme.Button(L.T("Save"), primary: true); save.Dock = DockStyle.Right; save.Width = 128; save.Margin = new Padding(0);
        save.Click += (_, _) =>
        {
            try { L.SetLanguage(language.SelectedIndex == 1 ? "vi" : "en"); DialogResult = DialogResult.OK; Close(); }
            catch (Exception error) { MessageBox.Show(this, L.T("Could not save language preference: ") + error.Message, L.T("Settings")); }
        };
        content.Controls.Add(save, 0, 3);
        card.Controls.Add(content); layout.Controls.Add(card, 0, 1);
        Controls.Add(layout);
        Theme.Apply(this);
        AcceptButton = save;
    }
}
