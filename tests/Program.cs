using System.Net;
using System.Text;
using System.Text.Json;
using RdpLauncher;

var passed = 0;
void Test(string name, Action test) { test(); passed++; Console.WriteLine($"PASS {name}"); }
async Task AsyncTest(string name, Func<Task> test) { await test(); passed++; Console.WriteLine($"PASS {name}"); }
void Assert(bool ok) { if (!ok) throw new Exception("Assertion failed"); }
void Reject(Action test) { try { test(); } catch (InvalidOperationException) { return; } throw new Exception("Should reject"); }
string Info(string ip = "100.88.1.2", string run = "42", string request = "abc", int protocol = 1, int hours = 1) =>
    JsonSerializer.Serialize(new { protocol, run_id = run, request_id = request, ip, username = "RDP", expires_at = DateTimeOffset.UtcNow.AddHours(hours) });
Test("accept matched session", () => Assert(GitHubClient.ParseConnection(Info(), 42, "rdp-connection-abc").Ip == "100.88.1.2"));
Test("reject public IP", () => Reject(() => GitHubClient.ParseConnection(Info(ip: "8.8.8.8"), 42, "rdp-connection-abc")));
Test("reject out of CGNAT range", () => Reject(() => GitHubClient.ParseConnection(Info(ip: "100.128.1.2"), 42, "rdp-connection-abc")));
Test("reject IPv6", () => Reject(() => GitHubClient.ParseConnection(Info(ip: "::1"), 42, "rdp-connection-abc")));
Test("reject mismatched run", () => Reject(() => GitHubClient.ParseConnection(Info(run: "43"), 42, "rdp-connection-abc")));
Test("reject mismatched request", () => Reject(() => GitHubClient.ParseConnection(Info(request: "other"), 42, "rdp-connection-abc")));
Test("reject expired session", () => Reject(() => GitHubClient.ParseConnection(Info(hours: -1), 42, "rdp-connection-abc")));
Test("reject unsupported protocol", () => Reject(() => GitHubClient.ParseConnection(Info(protocol: 2), 42, "rdp-connection-abc")));
Test("reject URL injection in repo", () => Reject(() => new GitHubClient("evil/repo?query=x", "test")));
await AsyncTest("dispatch correct branch and inputs; no secret in body", async () =>
{
    var count = 0;
    using var api = new GitHubClient("owner/repo", "test-token", new FakeHandler(async req =>
    {
        count++;
        Assert(req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == "/repos/owner/repo/actions/workflows/main.yml/dispatches");
        var text = await req.Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        Assert(doc.RootElement.GetProperty("ref").GetString() == "main");
        Assert(doc.RootElement.GetProperty("inputs").GetProperty("request_id").GetString() == "abc");
        Assert(!text.Contains("test-token"));
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }));
    await api.Dispatch("main", "abc", 60, CancellationToken.None);
    Assert(count == 1);
});
await AsyncTest("correlate exact title, ignore concurrent run", async () =>
{
    using var api = new GitHubClient("owner/repo", "test", new FakeHandler(_ => Task.FromResult(Json(new { workflow_runs = new[] {
        new { display_title = "rdp-app-other", id = 41 }, new { display_title = "rdp-app-abc", id = 42 } } }))));
    Assert(await api.FindRun("abc", CancellationToken.None) == 42);
});
await AsyncTest("preflight rejects missing secret before dispatch", async () =>
{
    using var api = new GitHubClient("owner/repo", "test", new FakeHandler(req =>
    {
        Assert(req.Method == HttpMethod.Get);
        var path = req.RequestUri!.AbsolutePath;
        object data = path switch
        {
            "/user" => new { login = "owner" },
            "/repos/owner/repo" => new { archived = false, fork = true, source = new { full_name = "hn24987/RDP" }, permissions = new { push = true }, default_branch = "main" },
            "/repos/owner/repo/actions/permissions" => new { enabled = true },
            "/repos/owner/repo/actions/workflows/main.yml" => new { state = "active" },
            "/repos/owner/repo/contents/.github/workflows/main.yml" => new { content = Convert.ToBase64String(Encoding.UTF8.GetBytes("workflow_dispatch: RDP_LAUNCHER_PROTOCOL_V1")) },
            "/repos/owner/repo/actions/workflows/main.yml/runs" => new { workflow_runs = Array.Empty<object>() },
            "/repos/owner/repo/actions/secrets" => new { secrets = new[] { new { name = "TAILSCALE_AUTH_KEY" } } },
            _ => throw new Exception("Unexpected request")
        };
        return Task.FromResult(Json(data));
    }));
    try { await api.Preflight(_ => { }, CancellationToken.None); }
    catch (InvalidOperationException ex) { Assert(ex.Message.Contains("RDP_PASSWORD")); return; }
    throw new Exception("Missing secret was accepted");
});
await AsyncTest("GitHub auth error does not leak response body", async () =>
{
    using var api = new GitHubClient("owner/repo", "test", new FakeHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("sensitive-response") })));
    try { await api.Get("actions/runs", CancellationToken.None); }
    catch (InvalidOperationException ex) { Assert(ex.Message.Contains("401") && !ex.Message.Contains("sensitive-response")); return; }
    throw new Exception("401 accepted");
});
await AsyncTest("download and validate ready artifact while job is still running", async () =>
{
    var zipBytes = new MemoryStream();
    using (var archive = new System.IO.Compression.ZipArchive(zipBytes, System.IO.Compression.ZipArchiveMode.Create, true))
    {
        using var writer = new StreamWriter(archive.CreateEntry("connection.json").Open());
        writer.Write(Info());
    }
    var calls = 0;
    using var api = new GitHubClient("owner/repo", "test", new FakeHandler(req =>
    {
        calls++;
        if (req.RequestUri!.AbsolutePath.EndsWith("/artifacts"))
            return Task.FromResult(Json(new { artifacts = new[] { new { id = 7, name = "rdp-connection-abc", expired = false, size_in_bytes = zipBytes.Length } } }));
        Assert(req.RequestUri.AbsolutePath.EndsWith("/actions/artifacts/7/zip"));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(zipBytes.ToArray()) });
    }));
    var info = await api.ReadConnection(42, CancellationToken.None);
    Assert(info?.Ip == "100.88.1.2" && calls == 2);
});
Test("normalize full fork URL", () => Assert(RepositoryAddress.Parse(" https://github.com/alice/RDP.git/ ") == "alice/RDP"));
Test("reject upstream source as target", () => Reject(() => RepositoryAddress.Parse("https://github.com/HN24987/rdp")));
Test("reject non-GitHub host", () => Reject(() => RepositoryAddress.Parse("https://github.com.evil.test/alice/RDP")));
Test("reject URL credentials", () => Reject(() => RepositoryAddress.Parse("https://token@github.com/alice/RDP")));
Test("reject plain owner/repo", () => Reject(() => RepositoryAddress.Parse("alice/RDP")));
Test("reject Actions URL", () => Reject(() => RepositoryAddress.Parse("https://github.com/alice/RDP/actions")));
Test("reject query parameters", () => Reject(() => RepositoryAddress.Parse("https://github.com/alice/RDP?token=x")));
Test("reject non-HTTPS fork URL", () => Reject(() => RepositoryAddress.Parse("http://github.com/alice/RDP")));
JsonElement Metadata(bool fork = true, string source = "hn24987/RDP", bool push = true, bool archived = false) =>
    JsonSerializer.SerializeToElement(new { fork, source = new { full_name = source }, permissions = new { push }, archived });
Test("accept writable RDP fork", () => GitHubClient.ValidateFork(Metadata()));
Test("reject copied repository", () => Reject(() => GitHubClient.ValidateFork(Metadata(fork: false))));
Test("reject unrelated fork network", () => Reject(() => GitHubClient.ValidateFork(Metadata(source: "someone/other"))));
Test("reject read-only fork access", () => Reject(() => GitHubClient.ValidateFork(Metadata(push: false))));
Test("reject archived fork", () => Reject(() => GitHubClient.ValidateFork(Metadata(archived: true))));
string State(string backend, bool online = false) => JsonSerializer.Serialize(new { BackendState = backend, Self = new { Online = online } });
Test("Tailscale ready", () => Assert(TailscaleCheck.StatusProblem(State("Running", true)) is null));
Test("Tailscale switched off", () => Assert(TailscaleCheck.StatusProblem(State("Stopped"))!.Contains("switched off")));
Test("Tailscale signed out", () => Assert(TailscaleCheck.StatusProblem(State("NeedsLogin"))!.Contains("not signed in")));
Test("Tailscale device approval", () => Assert(TailscaleCheck.StatusProblem(State("NeedsMachineAuth"))!.Contains("approval")));
Test("Tailscale running but offline", () => Assert(TailscaleCheck.StatusProblem(State("Running"))!.Contains("offline")));
Test("Tailscale missing self is not ready", () => Assert(TailscaleCheck.StatusProblem("{\"BackendState\":\"Running\"}") is not null));
await AsyncTest("preflight success uses GET only and returns default branch", async () =>
{
    using var api = new GitHubClient("owner/repo", "test", new FakeHandler(req =>
    {
        Assert(req.Method == HttpMethod.Get);
        object response = req.RequestUri!.AbsolutePath switch
        {
            "/user" => new { login = "owner" },
            "/repos/owner/repo" => new { archived = false, fork = true, source = new { full_name = "hn24987/RDP" }, permissions = new { push = true }, default_branch = "develop" },
            "/repos/owner/repo/actions/permissions" => new { enabled = true },
            "/repos/owner/repo/actions/workflows/main.yml" => new { state = "active" },
            "/repos/owner/repo/contents/.github/workflows/main.yml" => new { content = Convert.ToBase64String(Encoding.UTF8.GetBytes("workflow_dispatch: RDP_LAUNCHER_PROTOCOL_V1")) },
            "/repos/owner/repo/actions/secrets" => new { secrets = new[] { new { name = "TAILSCALE_AUTH_KEY" }, new { name = "RDP_PASSWORD" } } },
            "/repos/owner/repo/actions/workflows/main.yml/runs" => new { workflow_runs = Array.Empty<object>() },
            _ => throw new Exception("Unexpected request")
        };
        return Task.FromResult(Json(response));
    }));
    Assert(await api.Preflight(_ => { }, CancellationToken.None) == "develop");
});
Console.WriteLine($"{passed} tests passed.");
static HttpResponseMessage Json(object data) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json") };
sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request);
}
