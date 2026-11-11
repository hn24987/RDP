using System.Diagnostics;
using System.Text;

namespace RdpLauncher;

public sealed record TransferResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;
}

/// <summary>Receives the local Tailscale file inbox. The remote user sends selected files first.</summary>
public static class TaildropService
{
    const int OutputLimit = 16_384;

    public static ProcessStartInfo CreateReceiveStartInfo(string executable, string folder)
    {
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathFullyQualified(executable))
            throw new ArgumentException(L.T("Choose an absolute Tailscale executable path."), nameof(executable));
        if (string.IsNullOrWhiteSpace(folder) || folder.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0 ||
            !Path.IsPathFullyQualified(folder))
            throw new ArgumentException(L.T("Choose an absolute destination folder path."), nameof(folder));
        var target = Path.GetFullPath(folder);
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = target
        };
        info.ArgumentList.Add("file");
        info.ArgumentList.Add("get");
        info.ArgumentList.Add(target);
        return info;
    }

    public static async Task<TransferResult> ReceiveAsync(string folder, CancellationToken ct)
    {
        var executable = TailscaleCheck.FindExecutable() ??
            throw new InvalidOperationException(L.T("Tailscale is not installed or its CLI could not be found."));
        var startInfo = CreateReceiveStartInfo(executable, folder);
        if (!Directory.Exists(startInfo.WorkingDirectory))
            throw new DirectoryNotFoundException(L.T("The selected destination folder no longer exists."));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start()) throw new InvalidOperationException(L.T("Could not start the Tailscale file receiver."));
            var output = ReadBoundedAsync(process.StandardOutput, timeout.Token);
            var error = ReadBoundedAsync(process.StandardError, timeout.Token);
            await Task.WhenAll(output, error, process.WaitForExitAsync(timeout.Token));
            return new TransferResult(process.ExitCode, await output, await error);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(L.T("File retrieval timed out after 30 seconds. Check Tailscale and try receiving again. Files are not uploaded to GitHub."));
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }

    static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken ct)
    {
        var text = new StringBuilder();
        var buffer = new char[1024];
        var truncated = false;
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), ct);
            if (read == 0) break;
            var keep = Math.Min(read, OutputLimit - text.Length);
            if (keep > 0) text.Append(buffer, 0, keep);
            if (keep < read) truncated = true;
        }
        if (truncated) text.Append(L.T("\n[output truncated]"));
        return text.ToString().Trim();
    }
}
