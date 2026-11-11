using System.Reflection;
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
if (!L.F("Remaining: {0}","00:05:00").Contains("00:05:00")) throw new Exception("Localized formatting failed.");
apply.Invoke(null,new object[]{"en"});
Console.WriteLine($"PASS {en.Count} resource pairs, placeholder parity, English/Vietnamese resource loading and formatting.");
