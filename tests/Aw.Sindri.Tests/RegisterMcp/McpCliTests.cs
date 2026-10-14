using Aw.Kernel.Json;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
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
            new Features.RegisterMcp.RegisterMcpResponse(Core.Ids.RepoName.From("demo"), "/work/demo", ["mcp", "add"], Applied: false), AwJson.CreateOptions(SindriJsonContext.Default)));

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

        Assert.Equal(1, exit);
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
}
