using System.Drawing.Imaging;
using System.Reflection;
using RdpLauncher;
internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        Directory.CreateDirectory("ui-shots");
        var apply = typeof(L).GetMethod("Apply",BindingFlags.NonPublic|BindingFlags.Static)!;
        foreach(var language in new[]{"en","vi"})
        {
            apply.Invoke(null,new object[]{language});
            using var main = new LauncherForm();
            Capture(main,"main-"+language);
            using var settings = new SettingsForm();
            Capture(settings,"settings-"+language);
            using var token = new TokenDialog();
            Capture(token,"token-"+language);
        }
    }
    static void Capture(Form form,string name)
    {
        form.CreateControl();
        Layout(form);
        using var bitmap = new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
        bitmap.Save(Path.Combine("ui-shots",name+".png"),ImageFormat.Png);
        Console.WriteLine(name+": "+bitmap.Width+"x"+bitmap.Height);
    }
    static void Layout(Control parent)
    {
        parent.CreateControl(); parent.PerformLayout();
        foreach(Control child in parent.Controls) Layout(child);
        parent.PerformLayout();
    }
}
