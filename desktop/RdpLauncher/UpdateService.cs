using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RdpLauncher;

public sealed record UpdateInfo(Version Version, string Url, bool IsNewer);

/// <summary>Checks release metadata only. Never downloads or executes an update.</summary>
public sealed class UpdateService : IDisposable
{
    public const string Repository = "hn24987/RDP-Launcher";
    public const string LatestUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";
    readonly HttpClient http;

    public UpdateService(HttpMessageHandler? handler = null)
    {
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(30);
        http.MaxResponseContentBufferSize = 1_048_576;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RdpLauncher-UpdateCheck/1.0");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<UpdateInfo?> CheckAsync(Version currentVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        using var response = await http.GetAsync(LatestUrl, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(L.F("Update check failed (GitHub HTTP {0}). Check your connection or GitHub API limits and try again.", (int)response.StatusCode));
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return ParseRelease(document.RootElement, currentVersion);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(L.T("GitHub returned unreadable release metadata."), ex);
        }
    }

    public static UpdateInfo? ParseRelease(JsonElement release, Version currentVersion)
    {
        if (release.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException(L.T("GitHub returned invalid release metadata."));
        if (release.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True) return null;
        if (release.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True) return null;
        if (!release.TryGetProperty("tag_name", out var tagProperty) || tagProperty.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException(L.T("The release does not contain a version tag."));
        var tag = tagProperty.GetString()!;
        var numeric = tag.StartsWith('v') || tag.StartsWith('V') ? tag[1..] : tag;
        if (!Regex.IsMatch(numeric, @"^\d+\.\d+\.\d+(?:\.\d+)?$") || !Version.TryParse(numeric, out var version))
            throw new InvalidOperationException(L.T("The latest release does not contain a supported stable version tag."));
        if (!release.TryGetProperty("html_url", out var urlProperty) || urlProperty.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(urlProperty.GetString(), UriKind.Absolute, out var uri) ||
            uri.Scheme != "https" || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !uri.AbsolutePath.Equals($"/{Repository}/releases/tag/{Uri.EscapeDataString(tag)}", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(L.T("The release page URL is not a trusted project release URL."));
        return new UpdateInfo(version, uri.AbsoluteUri, Normalize(version) > Normalize(currentVersion));
    }

    static Version Normalize(Version version) => new(version.Major, version.Minor,
        Math.Max(0, version.Build), Math.Max(0, version.Revision));

    public void Dispose() => http.Dispose();
}
