using System.Reflection;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using RdpLauncher;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var resources = Path.Combine(root, "desktop/RdpLauncher/Resources");
if (!Directory.Exists(resources)) throw new Exception("Cannot locate localization resources.");
Dictionary<string,string> Read(string file) => XDocument.Load(Path.Combine(resources,file)).Root!.Elements("data").ToDictionary(x=>x.Attribute("name")!.Value,x=>x.Element("value")!.Value);
var en = Read("Strings.resx"); var vi = Read("Strings.vi.resx");
if (!en.Keys.Order().SequenceEqual(vi.Keys.Order())) throw new Exception("Resource key sets do not match.");
foreach (var key in en.Keys)
{
    var a = Regex.Matches(en[key], @"\{\d+(?:[^}]*)?\}").Select(m=>m.Value).Order();
    var b = Regex.Matches(vi[key], @"\{\d+(?:[^}]*)?\}").Select(m=>m.Value).Order();
    if (!a.SequenceEqual(b)) throw new Exception("Format placeholders differ: "+key);
}
// Apply culture without writing the real user's preference file.
var apply = typeof(L).GetMethod("Apply", BindingFlags.NonPublic|BindingFlags.Static)!;
apply.Invoke(null,new object[]{"en"});
if (L.T("Check setup") != "Check setup") throw new Exception("English resource did not load.");
apply.Invoke(null,new object[]{"vi"});
if (L.T("Check setup") == "Check setup") throw new Exception("Vietnamese satellite resource did not load.");
if (L.T("Your remote workspace.") == "Your remote workspace." || L.T("Not connected") == "Not connected") throw new Exception("Dashboard translations did not load.");
if (!L.F("Remaining: {0}","00:05:00").Contains("00:05:00")) throw new Exception("Localized formatting failed.");
apply.Invoke(null,new object[]{"en"});

var isolated = Path.Combine(Path.GetTempPath(), "rdp-language-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(isolated);
var preference = Path.Combine(isolated, "preferences.json");
L.PreferenceFileOverride = preference;
try
{
    CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");
    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
    L.Initialize();
    if (L.Language != "en" || CultureInfo.CurrentUICulture.Name != "en-US")
        throw new Exception("Fresh launch must default to English independently of the OS culture.");
    if (File.Exists(preference)) throw new Exception("Reading the default language must not create a preference file.");
    Console.WriteLine("PASS fresh launch defaults to English on a Vietnamese culture");

    File.WriteAllText(preference, "{\"otherPreference\":42}");
    L.SetLanguage("vi");
    using (var saved = JsonDocument.Parse(File.ReadAllText(preference)))
    {
        if (saved.RootElement.GetProperty("language").GetString() != "vi" || saved.RootElement.GetProperty("otherPreference").GetInt32() != 42)
            throw new Exception("Saving language must preserve unrelated preferences.");
    }
    apply.Invoke(null, new object[]{"en"});
    L.Initialize();
    if (L.Language != "vi") throw new Exception("An explicit Vietnamese choice must survive reinitialization.");
    Console.WriteLine("PASS Vietnamese choice persists and unrelated preferences are preserved");

    L.SetLanguage("en");
    apply.Invoke(null, new object[]{"vi"});
    L.Initialize();
    if (L.Language != "en") throw new Exception("An explicit English choice must survive reinitialization.");
    var before = File.ReadAllText(preference);
    try { L.SetLanguage("fr"); throw new Exception("Unsupported language accepted."); }
    catch (ArgumentOutOfRangeException) { }
    if (File.ReadAllText(preference) != before) throw new Exception("Rejected language mutated preferences.");
    Console.WriteLine("PASS English persists and unsupported choices do not mutate preferences");

    foreach (var malformed in new[]{"{broken", "[]", "{\"language\":42}", "{\"language\":\"fr\"}"})
    {
        File.WriteAllText(preference, malformed);
        L.Initialize();
        if (L.Language != "en" || File.ReadAllText(preference) != malformed)
            throw new Exception("Unreadable/unsupported saved language must fall back to English without overwriting the file.");
    }
    if (Directory.GetFiles(isolated).Any(path => path.EndsWith(".tmp"))) throw new Exception("Temporary settings files were left behind.");
    Console.WriteLine("PASS malformed saved preferences use English without being overwritten");
}
finally
{
    L.PreferenceFileOverride = null;
    apply.Invoke(null,new object[]{"en"});
    Directory.Delete(isolated,true);
}
Console.WriteLine($"PASS {en.Count} resource pairs, placeholder parity, English/Vietnamese resource loading and formatting.");
