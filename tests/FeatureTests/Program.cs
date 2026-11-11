using System.Net;
using System.Text;
using System.Text.Json;
using RdpLauncher;

var passed = 0;
void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Test(string name, Action test) { test(); Console.WriteLine("PASS " + name); passed++; }
async Task AsyncTest(string name, Func<Task> test) { await test(); Console.WriteLine("PASS " + name); passed++; }
void Reject(Action test)
{
    try { test(); }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { return; }
    throw new Exception("Expected rejection");
}
JsonElement Release(string tag = "v1.3.0", string? url = null, bool draft = false, bool prerelease = false) =>
    JsonSerializer.SerializeToElement(new { tag_name = tag, html_url = url ?? $"https://github.com/hn24987/RDP-Launcher/releases/tag/{tag}", draft, prerelease });

Test("newer stable release is available", () => Assert(UpdateService.ParseRelease(Release(), new Version(1, 2, 0))!.IsNewer));
Test("equal four-part current version is not an update", () => Assert(!UpdateService.ParseRelease(Release("v1.2.0"), new Version(1, 2, 0, 0))!.IsNewer));
Test("older release is not an update", () => Assert(!UpdateService.ParseRelease(Release("v1.1.1"), new Version(1, 2, 0))!.IsNewer));
Test("version comparison is numeric", () => Assert(UpdateService.ParseRelease(Release("v1.10.0"), new Version(1, 9, 0))!.IsNewer));
Test("stable tags without v prefix work", () => Assert(UpdateService.ParseRelease(Release("2.0.0"), new Version(1, 2, 0))!.Version == new Version(2, 0, 0)));
Test("draft releases are ignored", () => Assert(UpdateService.ParseRelease(Release(draft: true), new Version(1, 2, 0)) is null));
Test("prereleases are ignored", () => Assert(UpdateService.ParseRelease(Release(tag: "v1.3.0-beta.1", prerelease: true), new Version(1, 2, 0)) is null));
Test("malformed tag is rejected", () => Reject(() => UpdateService.ParseRelease(Release(tag: "latest"), new Version(1, 2, 0))));
Test("prerelease-looking tag cannot be parsed as stable", () => Reject(() => UpdateService.ParseRelease(Release(tag: "v1.3.0-beta"), new Version(1, 2, 0))));
Test("non-HTTPS release page rejected", () => Reject(() => UpdateService.ParseRelease(Release(url: "http://github.com/hn24987/RDP-Launcher/releases/tag/v1.3.0"), new Version(1, 2, 0))));
Test("unrelated release page rejected", () => Reject(() => UpdateService.ParseRelease(Release(url: "https://github.com/other/repo/releases/tag/v1.3.0"), new Version(1, 2, 0))));
Test("release URL credentials rejected", () => Reject(() => UpdateService.ParseRelease(Release(url: "https://secret@github.com/hn24987/RDP-Launcher/releases/tag/v1.3.0"), new Version(1, 2, 0))));
Test("release URL host lookalike rejected", () => Reject(() => UpdateService.ParseRelease(Release(url: "https://github.com.evil.test/hn24987/RDP-Launcher/releases/tag/v1.3.0"), new Version(1, 2, 0))));
Test("mismatched release tag URL rejected", () => Reject(() => UpdateService.ParseRelease(Release(url: "https://github.com/hn24987/RDP-Launcher/releases/tag/v9.9.9"), new Version(1, 2, 0))));
Test("missing version metadata rejected", () => Reject(() => UpdateService.ParseRelease(JsonSerializer.SerializeToElement(new { html_url = "https://github.com" }), new Version(1, 2, 0))));
await AsyncTest("update HTTP request uses public endpoint and user-agent", async () =>
{
    using var service = new UpdateService(new FakeHandler(req =>
    {
        Assert(req.RequestUri!.AbsoluteUri == UpdateService.LatestUrl);
        Assert(req.Headers.UserAgent.Count > 0 && req.Headers.Authorization is null);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Release().GetRawText(), Encoding.UTF8, "application/json") };
    }));
    Assert((await service.CheckAsync(new Version(1, 2, 0), CancellationToken.None))!.IsNewer);
});
await AsyncTest("no published release is handled as no result", async () =>
{
    using var service = new UpdateService(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
    Assert(await service.CheckAsync(new Version(1, 2, 0), CancellationToken.None) is null);
});
await AsyncTest("unreadable release response gives clear error", async () =>
{
    using var service = new UpdateService(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{invalid") }));
    try { await service.CheckAsync(new Version(1, 2, 0), CancellationToken.None); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Unreadable metadata accepted");
});
await AsyncTest("HTTP failure does not expose response body", async () =>
{
    using var service = new UpdateService(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("sensitive-body") }));
    try { await service.CheckAsync(new Version(1, 2, 0), CancellationToken.None); }
    catch (InvalidOperationException ex) { Assert(ex.Message.Contains("403") && !ex.Message.Contains("sensitive-body")); return; }
    throw new Exception("403 accepted");
});

var now = DateTimeOffset.Parse("2026-11-11T10:00:00Z");
Test("countdown has no active session initially", () => { var c = new SessionCountdown(); Assert(c.Tick(now).Count == 0 && c.Remaining(now) is null); });
Test("thresholds fire once with normal progression", () =>
{
    var c = new SessionCountdown(); c.Configure(1, now.AddMinutes(20), now);
    Assert(c.Tick(now.AddMinutes(9)).Count == 0);
    Assert(c.Tick(now.AddMinutes(10)).SequenceEqual(new[] { SessionWarning.TenMinutes }));
    Assert(c.Tick(now.AddMinutes(11)).Count == 0);
    Assert(c.Tick(now.AddMinutes(15)).SequenceEqual(new[] { SessionWarning.FiveMinutes }));
    Assert(c.Tick(now.AddMinutes(19)).Count == 0);
    Assert(c.Tick(now.AddMinutes(20)).SequenceEqual(new[] { SessionWarning.Expired }));
    Assert(c.Tick(now.AddMinutes(21)).Count == 0);
});
Test("late monitoring emits one five-minute warning", () =>
{
    var c = new SessionCountdown(); c.Configure(2, now.AddMinutes(4), now);
    Assert(c.Tick(now).SequenceEqual(new[] { SessionWarning.FiveMinutes }));
    Assert(c.Tick(now).Count == 0);
});
Test("late monitoring between thresholds emits ten-minute warning", () =>
{
    var c = new SessionCountdown(); c.Configure(3, now.AddMinutes(8), now);
    Assert(c.Tick(now).SequenceEqual(new[] { SessionWarning.TenMinutes }));
});
Test("clock jump below both thresholds does not produce duplicate popups", () =>
{
    var c = new SessionCountdown(); c.Configure(4, now.AddMinutes(20), now);
    Assert(c.Tick(now.AddMinutes(17)).SequenceEqual(new[] { SessionWarning.FiveMinutes }));
});
Test("expired metadata only emits expired", () =>
{
    var c = new SessionCountdown(); c.Configure(5, now.AddMinutes(-1), now);
    Assert(c.Tick(now).SequenceEqual(new[] { SessionWarning.Expired }));
    Assert(c.Remaining(now) == TimeSpan.Zero);
});
Test("resuming same run does not repeat warnings", () =>
{
    var c = new SessionCountdown(); c.Configure(6, now.AddMinutes(8), now);
    Assert(c.Tick(now).Count == 1); c.Clear();
    c.Configure(6, now.AddMinutes(8), now.AddMinutes(1));
    Assert(c.Tick(now.AddMinutes(1)).Count == 0);
});
Test("switching runs preserves warning history", () =>
{
    var c = new SessionCountdown(); c.Configure(7, now.AddMinutes(8), now); Assert(c.Tick(now).Count == 1);
    c.Configure(8, now.AddMinutes(8), now); Assert(c.Tick(now).Count == 1);
    c.Configure(7, now.AddMinutes(8), now); Assert(c.Tick(now).Count == 0);
});
Test("clear stops active notifications", () =>
{
    var c = new SessionCountdown(); c.Configure(9, now.AddMinutes(8), now); c.Clear();
    Assert(c.Tick(now).Count == 0 && c.ExpiresAt is null);
});
Test("invalid run IDs rejected", () => Reject(() => new SessionCountdown().Configure(0, now, now)));

var folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Taildrop folder & literal $(echo test)"));
var executable = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Tailscale program", "tailscale.exe"));
Test("Taildrop destination is one literal argument", () =>
{
    var p = TaildropService.CreateReceiveStartInfo(executable, folder);
    Assert(!p.UseShellExecute && p.CreateNoWindow && p.RedirectStandardOutput && p.RedirectStandardError);
    Assert(p.ArgumentList.SequenceEqual(new[] { "file", "get", folder }));
    Assert(p.Arguments == "" && p.FileName == executable && p.WorkingDirectory == folder);
});
Test("Taildrop rejects relative destination", () => Reject(() => TaildropService.CreateReceiveStartInfo(executable, "relative-folder")));
Test("Taildrop rejects multiline destination", () => Reject(() => TaildropService.CreateReceiveStartInfo(executable, folder + "\nother")));
Test("Taildrop rejects missing executable", () => Reject(() => TaildropService.CreateReceiveStartInfo("", folder)));
Test("Taildrop rejects relative executable", () => Reject(() => TaildropService.CreateReceiveStartInfo("tailscale.exe", folder)));
Console.WriteLine($"{passed} feature tests passed.");

sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> callback) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(callback(request));
}
