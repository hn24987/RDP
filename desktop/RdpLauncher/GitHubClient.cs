using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RdpLauncher;

public sealed record ConnectionInfo(string Ip, string Username, DateTimeOffset ExpiresAt);

public sealed class GitHubClient : IDisposable
{
    readonly HttpClient http;
    readonly string repo;
    public GitHubClient(string repository, string token, HttpMessageHandler? handler = null)
    {
        if (!Regex.IsMatch(repository, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$"))
            throw new InvalidOperationException("Repository must use the owner/repo format.");
        repo = $"repos/{repository}";
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.BaseAddress = new Uri("https://api.github.com/");
        http.Timeout = TimeSpan.FromSeconds(45);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RdpLauncher/1.1");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }
    public async Task<JsonElement> Request(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var hint = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Your token is invalid or expired. Use Change token to sign in again.",
                HttpStatusCode.Forbidden => "Check token permissions, organization approval/SSO, Actions policy, or API rate limits.",
                HttpStatusCode.NotFound => "Repository/workflow not found or your token does not have access. Select the repository when creating the token.",
                HttpStatusCode.UnprocessableEntity => "Check the branch, workflow_dispatch, and workflow inputs.",
                _ => "Open Actions / logs for details. If dispatch may have succeeded, use Resume run instead of creating another session."
            };
            throw new InvalidOperationException($"GitHub HTTP {(int)response.StatusCode}: {hint}");
        }
        if (response.StatusCode == HttpStatusCode.NoContent) return default;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.Clone();
    }
    public Task<JsonElement> Get(string suffix, CancellationToken ct) => Request(HttpMethod.Get, $"{repo}/{suffix}", null, ct);

    public async Task<string> GetIdentity(CancellationToken ct)
    {
        var user = await Request(HttpMethod.Get, "user", null, ct);
        return user.GetProperty("login").GetString()!;
    }

    public static void ValidateRepository(JsonElement metadata)
    {
        var isSource = metadata.TryGetProperty("full_name", out var fullName) &&
            RepositoryAddress.IsSource(fullName.GetString());
        if (!isSource)
        {
            if (!metadata.TryGetProperty("fork", out var fork) || !fork.GetBoolean())
                throw new InvalidOperationException("Use hn24987/RDP-Launcher if you have write access, or a fork of that project that you control.");
            if (!metadata.TryGetProperty("source", out var source) ||
                !RepositoryAddress.IsSource(source.GetProperty("full_name").GetString()))
                throw new InvalidOperationException("This fork does not belong to the hn24987/RDP-Launcher fork network.");
        }
        if (!metadata.TryGetProperty("permissions", out var permissions) ||
            !permissions.TryGetProperty("push", out var push) || !push.GetBoolean())
            throw new InvalidOperationException("Your GitHub account must have write access to this repository. Check the repository owner and token repository selection.");
        if (metadata.GetProperty("archived").GetBoolean())
            throw new InvalidOperationException("This repository is archived. Unarchive it before running workflows.");
        if (metadata.TryGetProperty("disabled", out var disabled) && disabled.GetBoolean())
            throw new InvalidOperationException("This repository is disabled by GitHub.");
    }

    public async Task<string> Preflight(Action<string> log, CancellationToken ct)
    {
        log("Checking GitHub authentication and repository…");
        var userTask = GetIdentity(ct);
        var metadataTask = Request(HttpMethod.Get, repo, null, ct);
        await Task.WhenAll(userTask, metadataTask);
        var metadata = await metadataTask;
        ValidateRepository(metadata);
        var branch = metadata.GetProperty("default_branch").GetString()!;
        log($"PASS · Signed in as {await userTask}; verified your RDP repository; default branch: {branch}");

        // Independent read-only checks run together to keep preflight quick.
        var policyTask = Get("actions/permissions", ct);
        var workflowTask = Get("actions/workflows/main.yml", ct);
        var sourceTask = Get($"contents/.github/workflows/main.yml?ref={Uri.EscapeDataString(branch)}", ct);
        var secretsTask = Get("actions/secrets?per_page=100&page=1", ct);
        var runsTask = Get("actions/workflows/main.yml/runs?per_page=100&page=1", ct);
        await Task.WhenAll(policyTask, workflowTask, sourceTask, secretsTask, runsTask);
        if (!(await policyTask).GetProperty("enabled").GetBoolean())
            throw new InvalidOperationException("Actions are disabled on this repository. Open Actions, enable workflows, and check Settings > Actions > General.");
        log("PASS · GitHub Actions is enabled");
        if ((await workflowTask).GetProperty("state").GetString() != "active")
            throw new InvalidOperationException("main.yml is not active. Open the repository's Actions tab and enable the workflow.");
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String((await sourceTask).GetProperty("content").GetString()!));
        if (!yaml.Contains("RDP_LAUNCHER_PROTOCOL_V1") || !yaml.Contains("workflow_dispatch:"))
            throw new InvalidOperationException("Your repository has an older workflow. Update .github/workflows/main.yml from this release.");
        log("PASS · Workflow is active and supports the launcher");
        var names = new HashSet<string>();
        var secrets = await secretsTask;
        for (var page = 1; ; page++)
        {
            var list = secrets.GetProperty("secrets");
            foreach (var secret in list.EnumerateArray()) names.Add(secret.GetProperty("name").GetString()!);
            if (list.GetArrayLength() < 100) break;
            secrets = await Get($"actions/secrets?per_page=100&page={page + 1}", ct);
        }
        var missing = new[] { "TAILSCALE_AUTH_KEY", "RDP_PASSWORD" }.Where(name => !names.Contains(name)).ToArray();
        if (missing.Length != 0)
            throw new InvalidOperationException($"Missing repository secrets: {string.Join(", ", missing)}. Open Secrets to add them; forked repositories do not inherit secrets.");
        log("PASS · TAILSCALE_AUTH_KEY and RDP_PASSWORD exist (values cannot be read back)");
        foreach (var run in (await runsTask).GetProperty("workflow_runs").EnumerateArray())
        {
            var status = run.GetProperty("status").GetString();
            if (status is not ("completed" or "cancelled"))
                log($"NOTICE · Existing run {run.GetProperty("id").GetInt64()} is {status}. Resume it if you do not need another machine.");
        }
        log("PASS · Workflow runs are readable. GitHub confirms Actions write permission when dispatching.");
        log("Runner availability, billing/quota, Auth Key validity, device approval and ACLs are confirmed only during startup/connection.");
        return branch;
    }
    public Task<JsonElement> Dispatch(string branch, string requestId, int minutes, CancellationToken ct) =>
        Request(HttpMethod.Post, $"{repo}/actions/workflows/main.yml/dispatches",
            new { @ref = branch, inputs = new { request_id = requestId, duration_minutes = minutes.ToString() } }, ct);

    public async Task<long?> FindRun(string requestId, CancellationToken ct)
    {
        // Exact run-name correlation prevents attaching to a concurrent user's session.
        for (var page = 1; page <= 5; page++)
        {
            var response = await Get($"actions/workflows/main.yml/runs?event=workflow_dispatch&per_page=100&page={page}", ct);
            var runs = response.GetProperty("workflow_runs");
            foreach (var run in runs.EnumerateArray())
                if (run.GetProperty("display_title").GetString() == $"rdp-app-{requestId}")
                    return run.GetProperty("id").GetInt64();
            if (runs.GetArrayLength() < 100) break;
        }
        return null;
    }
    public async Task<ConnectionInfo?> ReadConnection(long runId, CancellationToken ct)
    {
        var result = await Get($"actions/runs/{runId}/artifacts?per_page=100", ct);
        var artifacts = result.GetProperty("artifacts").EnumerateArray()
            .Where(x => x.GetProperty("name").GetString()!.StartsWith("rdp-connection-") && !x.GetProperty("expired").GetBoolean()).ToArray();
        if (artifacts.Length == 0) return null;
        if (artifacts.Length != 1) throw new InvalidOperationException("This run has multiple connection artifacts. Refusing to guess the session.");
        var artifact = artifacts[0];
        if (artifact.GetProperty("size_in_bytes").GetInt64() > 1_000_000) throw new InvalidOperationException("Artifact exceeds the allowed size.");
        var id = artifact.GetProperty("id").GetInt64();
        using var response = await http.GetAsync($"{repo}/actions/artifacts/{id}/zip", ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Could not download artifact: HTTP {(int)response.StatusCode}.");
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length > 1_000_000) throw new InvalidOperationException("Artifact exceeds the allowed size.");
        using var zip = new ZipArchive(new MemoryStream(bytes));
        var entry = zip.GetEntry("connection.json") ?? throw new InvalidOperationException("Artifact is missing connection.json.");
        if (entry.Length > 16384) throw new InvalidOperationException("connection.json exceeds the allowed size.");
        using var reader = new StreamReader(entry.Open());
        return ParseConnection(await reader.ReadToEndAsync(ct), runId, artifact.GetProperty("name").GetString()!);
    }
    public static ConnectionInfo ParseConnection(string json, long runId, string artifactName)
    {
        using var document = JsonDocument.Parse(json.TrimStart('\uFEFF'));
        var root = document.RootElement;
        if (root.GetProperty("protocol").GetInt32() != 1 || root.GetProperty("run_id").GetString() != runId.ToString() ||
            artifactName != "rdp-connection-" + root.GetProperty("request_id").GetString())
            throw new InvalidOperationException("Connection artifact does not match this session.");
        var ip = root.GetProperty("ip").GetString()!;
        if (!IPAddress.TryParse(ip, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new InvalidOperationException("Invalid IP address.");
        var octets = address.GetAddressBytes();
        if (octets[0] != 100 || octets[1] < 64 || octets[1] > 127)
            throw new InvalidOperationException("IP address is outside the Tailscale range.");
        var username = root.GetProperty("username").GetString();
        if (username != "RDP") throw new InvalidOperationException("Invalid RDP username.");
        var expires = root.GetProperty("expires_at").GetDateTimeOffset();
        if (expires <= DateTimeOffset.UtcNow) throw new InvalidOperationException("This RDP session has expired.");
        return new ConnectionInfo(ip, username, expires);
    }
    public Task<JsonElement> Cancel(long runId, CancellationToken ct) => Request(HttpMethod.Post, $"{repo}/actions/runs/{runId}/cancel", null, ct);
    public void Dispose() => http.Dispose();
}
