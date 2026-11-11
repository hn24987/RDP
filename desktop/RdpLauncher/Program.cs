namespace RdpLauncher;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        L.Initialize();
        ApplicationConfiguration.Initialize();
        Application.Run(new LauncherForm());
    }
}
