using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Infrastructure.Browser;
using Aw.Evidence.Testing;
using Aw.Evidence.Tests.Infrastructure;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Cli;

/// <summary>
/// A server redirect off the vetted origin, end to end through the real binary and Chromium. The route that aborts
/// off-origin navigations never sees a redirect hop, so the session checks where the page actually is.
/// </summary>
[Lane(TestLane.Heavy)]
public sealed class OffOriginRedirectTests : IAsyncLifetime
{
    private const string Password = "s3cret-pw";
    private readonly CliWorkspace _work = new();
    private readonly LocalHttpServer _elsewhere = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["/away"] = "<!doctype html><html><head><title>Away</title></head><body><p>Away</p></body></html>",
        ["/login"] = string.Join('\n',
            "<!doctype html><html><head><title>Sign in</title></head><body>",
            "<form method=\"post\" action=\"/session\">",
            "<input id=\"email\" name=\"email\"><input id=\"password\" name=\"password\" type=\"password\">",
            "<button type=\"submit\">Sign in</button>",
            "</form></body></html>"),
    });

    private LocalHttpServer? _app;

    private LocalHttpServer App => _app ?? throw new InvalidOperationException("not initialised");

    public async ValueTask InitializeAsync()
    {
        var installed = await new PlaywrightInstaller().InstallAsync(CancellationToken.None);
        Assert.True(installed.IsSuccess, installed.IsError ? installed.Error.Message : null);
        _app = new LocalHttpServer(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["/"] = _elsewhere.Origin + "/login", ["/go"] = _elsewhere.Origin + "/away" });
    }

    public ValueTask DisposeAsync()
    {
        _app?.Dispose();
        _elsewhere.Dispose();
        _work.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public void A_run_redirected_off_the_host_errors_with_exit_4_naming_both_origins()
    {
        var script = _work.Write("script.json", TestJson.Write(A.Script()
            .WithStep(new ScriptStep(StepAction.Goto, "/go", new TextVisible("Away"))).Build()));

        var (exit, output, error) = _work.Run("run", script, "run-1", "--host", App.Origin);

        Assert.Contains(_elsewhere.Received, static r => r.StartsWith("GET /away ", StringComparison.Ordinal));
        Assert.Equal((4, ""), (exit, output));
        Assert.Contains($"the page left {App.Origin} for {_elsewhere.Origin}", error, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_work.Root, "run-1", "summary.json")));
    }

    [Fact]
    public void Parity_never_types_credentials_into_a_page_a_redirect_took_off_the_host()
    {
        var manifest = _work.Write("manifest.json", TestJson.Write(A.Manifest().WithLogin(ManifestBuilder.Login()).Build()));
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal) { ["APP_EMAIL"] = "qa@example.com", ["APP_PASSWORD"] = Password };

        var (exit, output, error) = _work.RunWith(secrets, "parity", manifest, "p1", "--host", App.Origin);

        Assert.Contains(_elsewhere.Received, static r => r.StartsWith("GET /login ", StringComparison.Ordinal));
        Assert.DoesNotContain(_elsewhere.Received, static r => r.Contains(Password, StringComparison.Ordinal) || r.Contains("qa%40example.com", StringComparison.Ordinal));
        Assert.DoesNotContain(_elsewhere.Received, static r => r.StartsWith("POST ", StringComparison.Ordinal));
        Assert.Equal((4, ""), (exit, output));
        Assert.Contains($"the page left {App.Origin} for {_elsewhere.Origin}", error, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, error, StringComparison.Ordinal);
    }
}
