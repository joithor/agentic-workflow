using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>
/// 'workflow status' is a client of the control plane's instance routes (engine spec §6.3; review run 2,
/// architecture-strategist-r2-4): it resolves its reference through 'GET /workflow/instances?address=' as the write verbs
/// do, reads 'GET /workflow/instances/{id}', and never starts serve.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class StatusCommandTests
{
    [Theory(Timeout = 30_000)]
    [InlineData("T-3/step/1")]
    [InlineData("T-3")]
    public async Task Status_of_an_address_or_a_bare_item_prints_the_instance_through_the_routes(string reference)
    {
        using var api = ReadApi.OverAt(Instances(), new FakeTimeProvider(StatusCases.Now));

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, reference);

        Assert.Equal((0, string.Empty), (exit, stderr));
        Assert.Equal(StatusLayout.Render(StatusCases.View("done"), StatusCases.Now), stdout);   // the served 'current.status.text', which is the §6.4 layout
        Assert.Equal(2, api.Calls.Count);
        Assert.StartsWith("/sindri/workflow/instances?address=", api.Calls[0], StringComparison.Ordinal);
        Assert.Equal("/sindri/workflow/instances/" + StatusCases.View("done").Id.Value, api.Calls[1]);
    }

    [Fact(Timeout = 30_000)]
    public async Task Status_json_prints_the_served_status_fields()
    {
        using var api = ReadApi.OverAt(Instances(), new FakeTimeProvider(StatusCases.Now));

        var (exit, stdout, _) = await Run(api, TestContext.Current.CancellationToken, "T-3/step/1", "--json");

        Assert.Equal(0, exit);
        Assert.Equal(System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(InstanceViews.Status(StatusCases.View("done"), StatusCases.Now), SliceHarness.Json)) + "\n", stdout);
    }

    [Theory(Timeout = 30_000)]
    [InlineData("T-9")]
    [InlineData("T-9/step/1")]
    public async Task Status_of_an_unknown_instance_is_SND_WF_001_with_exit_2(string reference)
    {
        using var api = ReadApi.Over(Instances());

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, reference);

        Assert.Equal((2, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-WF-001 ", stderr, StringComparison.Ordinal);
    }

    [Theory(Timeout = 30_000)]
    [InlineData("status")]
    [InlineData("events")]
    public async Task A_reference_that_is_neither_an_address_nor_an_item_is_SND_WF_001_and_exit_2(string verb)
    {
        using var api = ReadApi.Over(Instances());
        var (stdout, stderr) = (new StringWriter(), new StringWriter());
        await using var services = new ServiceCollection().AddSingleton<TimeProvider>(new FakeTimeProvider(StatusCases.Now)).BuildServiceProvider();
        var output = new Output(stdout, stderr, SliceHarness.Json);

        var exit = verb == "status"
            ? await StatusCommand.Run(["not-an-address"], api.Desk, output, TestContext.Current.CancellationToken)
            : await EventsCommand.Run(["not-an-address"], api.Desk, services, output, TestContext.Current.CancellationToken);

        Assert.Equal((2, string.Empty), (exit, stdout.ToString()));
        Assert.StartsWith("SND-WF-001 ", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 30_000)]
    public async Task Status_with_no_control_plane_is_SND_API_001_with_exit_1_and_starts_nothing()
    {
        using var api = ReadApi.Down();

        var (exit, stdout, stderr) = await Run(api, TestContext.Current.CancellationToken, "T-3/step/1");

        Assert.Equal((1, string.Empty), (exit, stdout));
        Assert.StartsWith("SND-API-001 not running: ", stderr, StringComparison.Ordinal);
    }

    private static OneInstance Instances() => new(StatusCases.View("done"));

    private static async Task<(int Exit, string Stdout, string Stderr)> Run(ReadApi api, CancellationToken ct, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await StatusCommand.Run(args, api.Desk, new Output(stdout, stderr, SliceHarness.Json), ct);

        return (exit, stdout.ToString(), stderr.ToString());
    }
}
