using System.Text;
using RdpLauncher;

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Run this test on Windows to exercise DPAPI.");
var folder = Path.Combine(Path.GetTempPath(), "RdpLauncher-token-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var path = Path.Combine(folder, "github-token.dat");
try
{
    const string first = "test-only-not-a-real-github-token";
    TokenStore.Save(first, path);
    if (Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(first)) throw new Exception("Token was saved in plaintext.");
    if (TokenStore.Load(path) != first) throw new Exception("DPAPI round trip failed.");
    TokenStore.Save("replacement-test-token", path);
    if (TokenStore.Load(path) != "replacement-test-token") throw new Exception("Replacing a token failed.");
    TokenStore.Delete(path);
    if (File.Exists(path)) throw new Exception("Forget token did not delete the file.");
    if (Directory.GetFiles(folder).Length != 0) throw new Exception("Temporary token files were left behind.");
    Console.WriteLine("PASS Windows DPAPI round trip, encryption at rest, replacement, deletion and temporary-file cleanup.");
}
finally { Directory.Delete(folder, true); }
