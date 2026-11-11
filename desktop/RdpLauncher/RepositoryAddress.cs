using System.Text.RegularExpressions;

namespace RdpLauncher;

public static class RepositoryAddress
{
    public const string Source = "hn24987/RDP-Launcher";
    public const string SourceUrl = "https://github.com/" + Source;
    public const string LegacySource = "hn24987/RDP";
    public static bool IsSource(string? name) =>
        string.Equals(name, Source, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, LegacySource, StringComparison.OrdinalIgnoreCase);
    public static string Parse(string text)
    {
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) || !uri.IsDefaultPort ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("Enter your repository URL: https://github.com/YOUR-USERNAME/RDP");
        var path = uri.AbsolutePath.TrimEnd('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) path = path[..^4];
        if (!Regex.IsMatch(path, @"^/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
            throw new InvalidOperationException("Use the repository URL, not an Actions, branch, or file URL.");
        var repo = path[1..];
        return repo;
    }
}
