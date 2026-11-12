using System.Drawing.Imaging;
using System.Reflection;
using RdpLauncher;

internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        Directory.CreateDirectory("ui-shots");
        var apply=typeof(L).GetMethod("Apply",BindingFlags.NonPublic|BindingFlags.Static)!;
        L.Initialize();
        if(L.Language!="en") throw new Exception("A clean first launch must default to English.");
        foreach(var language in new[]{"en","vi"})
        {
            apply.Invoke(null,new object[]{language});
            Capture(new LauncherForm(),"main-"+language);
            Capture(new SettingsForm(),"settings-"+language);
            Capture(new TokenDialog(),"token-"+language);
        }
    }
    static void Capture(Form form,string name)
    {
        using(form)
        using(var timer=new System.Windows.Forms.Timer{Interval=650})
        {
            int ticks=0;
            timer.Tick+=(_,_)=>{
                if(form is LauncherForm)
                {
                    var dialog=Application.OpenForms.OfType<TokenDialog>().FirstOrDefault();
                    if(dialog is not null){Save(dialog,name+"-first-launch");dialog.Close();ticks=0;return;}
                }
                if(++ticks<2)return;
                timer.Stop();Save(form,name);form.Close();
            };
            form.StartPosition=FormStartPosition.CenterScreen;
            timer.Start();Application.Run(form);
        }
    }
    static void Save(Form form,string name)
    {
        form.PerformLayout();
        using var bitmap=new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
        bitmap.Save(Path.Combine("ui-shots",name+".png"),ImageFormat.Png);
        Console.WriteLine(name+": "+bitmap.Width+"x"+bitmap.Height);
    }
}
