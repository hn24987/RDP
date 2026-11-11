using System.Diagnostics;
using System.Text.Json;

namespace RdpLauncher;

public static class TailscaleCheck
{
    public static string? FindExecutable(string file = "tailscale.exe")
    {
        var folders = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetEnvironmentVariable("ProgramW6432"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        }.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Path.Combine(p!, "Tailscale"))
         .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator));
        return folders.Select(p => Path.Combine(p.Trim('"'), file)).FirstOrDefault(File.Exists);
    }
    public static string? StatusProblem(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var state = root.TryGetProperty("BackendState", out var backend) ? backend.GetString() : "Unknown";
        if (state == "NeedsLogin") return L.T("Tailscale is installed but not signed in. Open Tailscale and sign in to the same tailnet as your Auth Key.");
        if (state == "NeedsMachineAuth") return L.T("This device needs approval. Approve it in the Tailscale admin console, then retry.");
        if (state == "Stopped") return L.T("Tailscale is installed but switched off. Open Tailscale, choose Connect, then retry.");
        if (state != "Running") return L.F("Tailscale is not ready (state: {0}). Open Tailscale, check its service, and connect.", state);
        if (!root.TryGetProperty("Self", out var self) || !self.TryGetProperty("Online", out var online) || !online.GetBoolean())
            return L.T("Tailscale is running but this device is offline. Check your network and reconnect Tailscale.");
        return null;
    }
    public static async Task Check(Action<string> log, CancellationToken ct)
    {
        var path = FindExecutable() ?? throw new InvalidOperationException(L.T("Tailscale is not installed or its CLI could not be found. Click Install Tailscale, install it, and sign in."));
        log(L.T("PASS · Tailscale is installed"));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        using var process = Process.Start(new ProcessStartInfo(path, "status --json")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true })!;
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            await stderr;
            // Some CLI versions return nonzero even with useful state JSON.
            if (!string.IsNullOrWhiteSpace(output))
            {
                string? problem;
                try { problem = StatusProblem(output); }
                catch (JsonException) { throw new InvalidOperationException(L.T("Tailscale returned an unreadable status. Open Tailscale and check its service/login.")); }
                if (problem is not null) throw new InvalidOperationException(problem);
            }
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException(L.T("Tailscale is installed but its service is unavailable or you are signed out. Open Tailscale, start its service/connect, then retry."));
            log(L.T("PASS · Tailscale service is responding, connected and online. Use the same tailnet as the runner Auth Key."));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new InvalidOperationException(L.T("Tailscale is installed but did not respond within 12 seconds. Open Tailscale and check that its service is running.")); }
        finally { if (!process.HasExited) process.Kill(true); }
    }
}
