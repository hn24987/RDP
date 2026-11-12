using System.Globalization;
using System.Resources;
using System.Text.Json;

namespace RdpLauncher;

/// <summary>Resource-backed UI text and the user's persistent language choice.</summary>
public static class L
{
    static readonly ResourceManager Resources = new("RdpLauncher.Resources.Strings", typeof(L).Assembly);
    public static string Language { get; private set; } = "en";
    // Tests compile this source into an isolated assembly and supply a temporary file.
    // Production callers leave this unset; no environment variable redirects credentials/settings.
    internal static string? PreferenceFileOverride { get; set; }
    static string PreferencePath => PreferenceFileOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RdpLauncher", "preferences.json");

    public static void Initialize()
    {
        var language = "en";
        try
        {
            if (File.Exists(PreferencePath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(PreferencePath));
                if (document.RootElement.TryGetProperty("language", out var saved) && saved.GetString() == "vi") language = "vi";
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { }
        Apply(language);
    }

    public static void SetLanguage(string language)
    {
        if (language is not ("en" or "vi")) throw new ArgumentOutOfRangeException(nameof(language));
        var preferences = new Dictionary<string, JsonElement>();
        try
        {
            if (File.Exists(PreferencePath))
                preferences = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(PreferencePath)) ?? preferences;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
        preferences["language"] = JsonSerializer.SerializeToElement(language);
        Directory.CreateDirectory(Path.GetDirectoryName(PreferencePath)!);
        var temporary = PreferencePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences));
            File.Move(temporary, PreferencePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Apply(language);
    }

    static void Apply(string language)
    {
        Language = language;
        var culture = CultureInfo.GetCultureInfo(language == "vi" ? "vi-VN" : "en-US");
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    public static string T(string key)
    {
        try { return Resources.GetString(key, CultureInfo.GetCultureInfo(Language)) ?? key; }
        catch (MissingManifestResourceException) { return key; }
    }
    public static string F(string key, params object?[] values) => string.Format(CultureInfo.CurrentCulture, T(key), values);
}
