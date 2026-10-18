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
            new RegisterMcpResponse(Core.Ids.RepoName.From("demo"), "/work/demo", ["mcp", "add"], Applied: false), AwJson.CreateOptions(SindriJsonContext.Default)));

        Assert.Equal("""{"repo":"demo","checkout":"/work/demo","arguments":["mcp","add"],"applied":false}""", json);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("CLAUDECODE is set", true)]
    [InlineData(null, false)]
    public async Task Apply_from_a_session_or_without_a_terminal_is_SND_AUTH_002_before_anything_runs(string? signal, bool terminal)
    {
        using var error = new StringWriter();
        await using var services = new ServiceCollection().BuildServiceProvider();   // why: empty, so resolving the service would throw
        var output = new Output(TextWriter.Null, error, AwJson.CreateOptions(SindriJsonContext.Default));

        var exit = await new McpVerbs(services, output, new AgentSession(signal, terminal, Prover: false)).Run(["register", "--apply"], TestContext.Current.CancellationToken);

        Assert.Equal(1, exit);   // why: a refused input is attention (context spec §8.6), as the context map verbs answer it
        Assert.StartsWith("SND-AUTH-002", error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("register", "--bogus")]
    [InlineData("register", "--repo")]
    [InlineData("list")]
    public async Task Unknown_forms_are_usage_errors(params string[] args)
    {
        using var error = new StringWriter();
        await using var services = new ServiceCollection().BuildServiceProvider();
        var output = new Output(TextWriter.Null, error, AwJson.CreateOptions(SindriJsonContext.Default));

        var exit = await new McpVerbs(services, output, new AgentSession(Signal: null, Terminal: true, Prover: false)).Run(args, TestContext.Current.CancellationToken);

        Assert.Equal(2, exit);
        Assert.Contains("mcp register [--repo NAME] [--apply] [--json]", error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(new[] { "register", "--repo", "demo" }, 0, "claude mcp add --transport http --scope local sindri http://127.0.0.1:7444/sindri/mcp --header 'X-Sindri-Repo: demo'", null)]
    [InlineData(new[] { "register", "--json", "--repo", "demo" }, 0, "\"arguments\":[\"mcp\",\"add\"", null)]
    [InlineData(new[] { "register", "--repo", "nope" }, 1, null, "SND-CONTEXT-024 'nope' is not a repo the profile names")]
    [InlineData(new[] { "register", "--repo", "demo", "--apply" }, 2, null, "SND-CONTEXT-025 claude exited 1: already exists")]
    public async Task The_verb_wires_the_service_its_output_and_its_exit_codes(string[] args, int exit, string? stdout, string? stderr)
    {
        var profiles = new InMemoryProfiles();
        var live = SliceHarness.Profile();
        _ = await profiles.Save(live with { Profile = live.Profile with { Repos = [new RepoEntry("demo", "/work/demo")], Api = new ApiSettings(7444) } }, TestContext.Current.CancellationToken);
        var service = new RegisterMcpService(profiles, new FakeRepoLocator(), new InMemoryEndpointFile(), new FakeClaudeCli { Answer = new ClaudeRun(1, "already exists") });
        await using var services = new ServiceCollection().AddSingleton<IService<RegisterMcpRequest, RegisterMcpResponse>>(service).BuildServiceProvider();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = await new McpVerbs(services, new Output(output, error, AwJson.CreateOptions(SindriJsonContext.Default)), new AgentSession(Signal: null, Terminal: true, Prover: false))
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
