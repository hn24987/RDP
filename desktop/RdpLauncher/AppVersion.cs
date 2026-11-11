using System.Reflection;

namespace RdpLauncher;

public static class AppVersion
{
    public static Version Current { get; } = Version.Parse(
        (Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0)).ToString(3));
}
