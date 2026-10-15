using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>
/// 'workflow list' and 'workflow events' are clients of the control plane's routes, like 'status' (engine spec §6.3,
/// review run 2, architecture-strategist-r2-4): they never start serve, and with none running they exit 1 with SND-API-001.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ReadVerbsClientTests
{
    [Theory(Timeout = 30_000)]
    [InlineData("list")]
    [InlineData("events", "T-3/step/1")]
    [InlineData("events", "T-3/step/1", "--follow")]
    public async Task With_no_control_plane_the_read_verb_is_SND_API_001_with_exit_1_and_starts_nothing(params string[] args)
    {
        using var api = ReadApi.Down();

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, args);

        Assert.Equal((1, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-API-001 not running: ", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task List_sends_each_filter_to_the_route_and_prints_what_it_answers()
    {
        using var api = ReadApi.Over(new OneInstance(StatusCases.View("done")));

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, "list", "--item", "T-3", "--state", "done", "--query", "NeedsYou = false");

        Assert.Equal((0, "no instances match\n", string.Empty), (exit, stdout, stderr));
        Assert.Equal(["/sindri/workflow/instances?item=T-3&state=done&query=NeedsYou%20%3D%20false"], api.Calls);
    }

    [Fact(Timeout = 30_000)]
    public async Task List_with_a_state_that_is_not_a_state_name_is_refused_by_the_route_as_SND_CLI_003_with_exit_2()
    {
        using var api = ReadApi.Over(new OneInstance(StatusCases.View("done")));

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, "list", "--state", "Not A State");

        Assert.Equal((2, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-CLI-003 ", stderr, StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task List_needs_me_asks_for_needsMe()
    {
        using var api = ReadApi.Over(new OneInstance(StatusCases.View("done")));

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "list", "--needs-me");

        Assert.Equal((0, "nothing needs you\n"), (exit, stdout));
        Assert.Equal(["/sindri/workflow/instances?needsMe=true"], api.Calls);
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> Run(ReadApi api, CancellationToken ct, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        await using var services = new ServiceCollection().AddSingleton<TimeProvider>(new FakeTimeProvider(StatusCases.Now)).BuildServiceProvider();
        var output = new Output(stdout, stderr, SliceHarness.Json);

        var exit = args[0] == "list" ? await ListCommand.Run(args[1..], api.Desk, services, output, ct) : await EventsCommand.Run(args[1..], api.Desk, services, output, ct);

        return (exit, stdout.ToString(), stderr.ToString());
    }
}
