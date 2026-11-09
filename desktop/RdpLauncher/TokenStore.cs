using System.Security.Cryptography;
using System.Text;

namespace RdpLauncher;

public static class TokenStore
{
    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "github-token.dat");
    public static bool Exists => File.Exists(FilePath);
    public static string Load(string? filePath = null)
    {
        var bytes = ProtectedData.Unprotect(File.ReadAllBytes(filePath ?? FilePath), null, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public static void Save(string token, string? filePath = null)
    {
        var target = filePath ?? FilePath;
        var plain = Encoding.UTF8.GetBytes(token.Trim());
        try
        {
            var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
            var temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllBytes(temp, encrypted); File.Move(temp, target, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public static void Delete(string? filePath = null) { var target = filePath ?? FilePath; if (File.Exists(target)) File.Delete(target); }
}
