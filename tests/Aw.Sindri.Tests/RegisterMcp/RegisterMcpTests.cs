using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RegisterMcp;

[Lane(TestLane.Unit)]
public sealed class RegisterMcpTests
{
    private readonly InMemoryProfiles _profiles = new();
    private readonly FakeRepoLocator _locator = new FakeRepoLocator().In("/work/demo-wt/src", "/work/demo");
    private readonly InMemoryEndpointFile _endpoint = new();
    private readonly FakeClaudeCli _claude = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task From_a_worktree_it_prints_the_local_scope_command_for_the_main_checkout_on_the_running_port()
    {
        await Approve(apiPort: null);
        _ = await _endpoint.Write(new ControlEndpoint(new Uri("http://127.0.0.1:7495/"), 42, 1), Token);

        var registration = (await Service().Handle(new RegisterMcpRequest("/work/demo-wt/src", Repo: null, Apply: false), Token)).Value;

        Assert.Equal("demo", registration.Repo.Value);
        Assert.Equal("/work/demo", registration.Checkout);
        Assert.Equal(["mcp", "add", "--transport", "http", "--scope", "local", "sindri", "http://127.0.0.1:7495/sindri/mcp", "--header", "X-Sindri-Repo: demo"],
            registration.Arguments);
        Assert.False(registration.Applied);
        Assert.Empty(_claude.Runs);
    }

    [Fact]
    public void The_server_name_and_url_come_before_the_header_because_claude_reads_every_later_argument_as_a_header()
    {
        var arguments = McpRegistration.Arguments(RepoName.From("demo"), 7495);

        var header = arguments.ToList().IndexOf("--header");
        Assert.True(header > arguments.ToList().IndexOf(McpScope.ServerName), "the name must precede the variadic --header");
        Assert.Equal(arguments.Count - 2, header);   // why: claude mcp add's -H <header...> swallows every argument after it
    }

    [Fact]
    public async Task With_nothing_running_the_port_is_the_profile_api_port_else_the_default()
    {
        await Approve(apiPort: 7444);
        var profilePort = (await Service().Handle(new RegisterMcpRequest("/elsewhere", "demo", Apply: false), Token)).Value;
        await Approve(apiPort: null);
        var defaultPort = (await Service().Handle(new RegisterMcpRequest("/elsewhere", "demo", Apply: false), Token)).Value;

        Assert.EndsWith("127.0.0.1:7444/sindri/mcp", Url(profilePort.Arguments), StringComparison.Ordinal);
        Assert.EndsWith($"127.0.0.1:{ApiSettings.DefaultPort}/sindri/mcp", Url(defaultPort.Arguments), StringComparison.Ordinal);
    }

    private static string Url(IReadOnlyList<string> arguments) => arguments.Single(a => a.StartsWith("http://", StringComparison.Ordinal));

    [Fact]
    public async Task Outside_every_profile_repo_or_with_an_unknown_repo_it_is_024()
    {
        await Approve(apiPort: null);

        var outside = await Service().Handle(new RegisterMcpRequest("/elsewhere", Repo: null, Apply: false), Token);
        var unknown = await Service().Handle(new RegisterMcpRequest("/work/demo", "nope", Apply: false), Token);

        Assert.Equal("SND-CONTEXT-024", outside.Error.Code);
        Assert.Equal("SND-CONTEXT-024", unknown.Error.Code);
        Assert.Contains("one of: demo", unknown.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_runs_claude_once_in_the_main_checkout()
    {
        await Approve(apiPort: 7444);

        var applied = (await Service().Handle(new RegisterMcpRequest("/work/demo-wt/src", Repo: null, Apply: true), Token)).Value;

        Assert.True(applied.Applied);
        var run = Assert.Single(_claude.Runs);
        Assert.Equal(applied.Arguments, run.Arguments);
        Assert.Equal("/work/demo", run.Directory);
    }

    [Fact]
    public async Task A_failing_or_missing_claude_is_025()
    {
        await Approve(apiPort: null);
        _claude.Answer = new ClaudeRun(1, "MCP server sindri already exists in local config");
        var failed = await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true), Token);
        _claude.Answer = null;
        var missing = await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true), Token);

        Assert.Equal("SND-CONTEXT-025", failed.Error.Code);
        Assert.Contains("already exists", failed.Error.Message, StringComparison.Ordinal);
        Assert.Equal("SND-CONTEXT-025", missing.Error.Code);
        _claude.Answer = new ClaudeRun(1, string.Empty);
        Assert.StartsWith("claude exited 1 (fix:", (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true), Token)).Error.Message, StringComparison.Ordinal);
    }

    private RegisterMcpService Service() => new(_profiles, _locator, _endpoint, _claude);

    private async Task Approve(int? apiPort)
    {
        var live = SliceHarness.Profile();
        var profile = live.Profile with { Repos = [new RepoEntry("demo", "/work/demo")], Api = apiPort is { } port ? new ApiSettings(port) : null };
        _ = await _profiles.Save(live with { Profile = profile }, Token);
    }
}
