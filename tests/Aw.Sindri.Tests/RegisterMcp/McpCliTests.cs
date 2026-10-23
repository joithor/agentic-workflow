using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.RegisterMcp;

public sealed class McpCliTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_printed_line_quotes_only_the_arguments_that_need_it()
    {
        var line = McpVerbs.Line(["mcp", "add", "--header", "X-Sindri-Repo: it's", "sindri", "http://127.0.0.1:7410/sindri/mcp"]);

        Assert.Equal("claude mcp add --header 'X-Sindri-Repo: it'\\''s' sindri http://127.0.0.1:7410/sindri/mcp", line);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_json_answer_is_in_the_host_context()
    {
        var json = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(
            new RegisterMcpResponse(Core.Ids.RepoName.From("demo"), "/work/demo", ["mcp", "add"], Applied: false, [new ProviderResult(ProviderFile.Codex, ProviderOutcome.NeedsTrust, "x", Fix: null)]), AwJson.CreateOptions(SindriJsonContext.Default)));

        Assert.Equal("""{"repo":"demo","checkout":"/work/demo","arguments":["mcp","add"],"applied":false,"providers":[{"provider":"codex","outcome":"needsTrust","detail":"x","fix":null}]}""", json);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Apply_is_not_refused_whoever_runs_it()
    {
        var profiles = new InMemoryProfiles();
        var live = SliceHarness.Profile();
        _ = await profiles.Save(live with { Profile = live.Profile with { Repos = [new RepoEntry("demo", "/work/demo")] } }, TestContext.Current.CancellationToken);
        var service = new RegisterMcpService(profiles, new FakeRepoLocator(), new InMemoryEndpointFile(), new FakeClaudeCli(), new FakeProviderHost());
        await using var services = new ServiceCollection().AddSingleton<IService<RegisterMcpRequest, RegisterMcpResponse>>(service).BuildServiceProvider();
        using var error = new StringWriter();

        _ = await new McpVerbs(services, new Output(TextWriter.Null, error, AwJson.CreateOptions(SindriJsonContext.Default)))
            .Run(["register", "--repo", "demo", "--apply"], TestContext.Current.CancellationToken);

        Assert.DoesNotContain("SND-AUTH-002", error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(new[] { "register", "--repo", "demo", "--providers", "all" }, "claude: would register", "codex: would register")]
    [InlineData(new[] { "register", "--repo", "demo", "--providers", "codex,cursor" }, "cursor: would register", "codex: would register")]
    public async Task Providers_selects_which_lines_are_printed(string[] args, string first, string second)
    {
        var profiles = new InMemoryProfiles();
        var live = SliceHarness.Profile();
        _ = await profiles.Save(live with { Profile = live.Profile with { Repos = [new RepoEntry("demo", "/work/demo")] } }, TestContext.Current.CancellationToken);
        var service = new RegisterMcpService(profiles, new FakeRepoLocator(), new InMemoryEndpointFile(), new FakeClaudeCli(), new FakeProviderHost());
        await using var services = new ServiceCollection().AddSingleton<IService<RegisterMcpRequest, RegisterMcpResponse>>(service).BuildServiceProvider();
        using var output = new StringWriter();

        var code = await new McpVerbs(services, new Output(output, TextWriter.Null, AwJson.CreateOptions(SindriJsonContext.Default)))
            .Run(args, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains(first, output.ToString(), StringComparison.Ordinal);
        Assert.Contains(second, output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(args[^1] == "all" ? "never" : "claude:", output.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("register", "--bogus")]
    [InlineData("register", "--repo")]
    [InlineData("register", "--providers", "vim")]
    [InlineData("list")]
    public async Task Unknown_forms_are_usage_errors(params string[] args)
    {
        using var error = new StringWriter();
        await using var services = new ServiceCollection().BuildServiceProvider();
        var output = new Output(TextWriter.Null, error, AwJson.CreateOptions(SindriJsonContext.Default));

        var exit = await new McpVerbs(services, output).Run(args, TestContext.Current.CancellationToken);

        Assert.Equal(2, exit);
        Assert.Contains("mcp register [--repo NAME] [--providers claude,codex,cursor|all] [--apply] [--json]", error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(new[] { "register", "--repo", "demo" }, 0, "claude mcp add --transport http --scope local sindri http://127.0.0.1:7444/sindri/mcp --header 'X-Sindri-Repo: demo'", null)]
    [InlineData(new[] { "register", "--json", "--repo", "demo" }, 0, "\"arguments\":[\"mcp\",\"add\"", null)]
    [InlineData(new[] { "register", "--repo", "nope" }, 1, null, "SND-CONTEXT-024 'nope' is not a repo the profile names")]
    [InlineData(new[] { "register", "--repo", "demo", "--apply" }, 2, null, "SND-CONTEXT-025 claude: claude exited 1: already exists")]
    public async Task The_verb_wires_the_service_its_output_and_its_exit_codes(string[] args, int exit, string? stdout, string? stderr)
    {
        var profiles = new InMemoryProfiles();
        var live = SliceHarness.Profile();
        _ = await profiles.Save(live with { Profile = live.Profile with { Repos = [new RepoEntry("demo", "/work/demo")], Api = new ApiSettings(7444) } }, TestContext.Current.CancellationToken);
        var service = new RegisterMcpService(profiles, new FakeRepoLocator(), new InMemoryEndpointFile(), new FakeClaudeCli { Answer = new ClaudeRun(1, "already exists") }, new FakeProviderHost());
        await using var services = new ServiceCollection().AddSingleton<IService<RegisterMcpRequest, RegisterMcpResponse>>(service).BuildServiceProvider();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = await new McpVerbs(services, new Output(output, error, AwJson.CreateOptions(SindriJsonContext.Default)))
            .Run(args, TestContext.Current.CancellationToken);

        Assert.Equal(exit, code);
        if (stdout is not null)
        {
            Assert.Contains(stdout, output.ToString(), StringComparison.Ordinal);
        }

        if (stderr is not null)
        {
            Assert.StartsWith(stderr, error.ToString(), StringComparison.Ordinal);
        }
    }
}
