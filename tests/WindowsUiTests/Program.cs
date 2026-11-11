using System.Reflection;
using RdpLauncher;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var apply = typeof(L).GetMethod("Apply",BindingFlags.Static|BindingFlags.NonPublic)!;
        foreach (var language in new[]{"en","vi"})
        {
            apply.Invoke(null,new object[]{language});
            using var form = new LauncherForm();
            form.CreateControl();
            foreach (var size in new[]{new System.Drawing.Size(1020,900),new System.Drawing.Size(860,720)})
            {
                form.Size = size;
                form.PerformLayout();
                var all = All(form).ToArray();
                var expected=L.T("Check for updates");
                if (!all.OfType<Button>().Any(b=>b.Text==expected)) throw new Exception("Update button not localized: "+language);
                if (!all.OfType<Button>().Any(b=>b.Text==L.T("Receive files"))) throw new Exception("Missing Receive files button");
            }
            using var settings = new SettingsForm(); settings.CreateControl(); settings.PerformLayout();
            using var dialog = new TokenDialog(); dialog.CreateControl(); dialog.PerformLayout();
            form.Dispose(); // Explicit double-dispose verifies timer/CTS cleanup is idempotent.
            Console.WriteLine("PASS WinForms construction/layout/settings/token dialogs in "+language);
        }
    }
    static IEnumerable<Control> All(Control parent)
    {
        foreach (Control control in parent.Controls) { yield return control; foreach (var nested in All(control)) yield return nested; }
    }
}
