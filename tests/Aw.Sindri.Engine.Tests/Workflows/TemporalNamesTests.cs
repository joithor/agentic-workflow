using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.ApplyCommand;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The namespace and task queue are settings, not constants (README X61): AW_SINDRI_TEMPORAL_NAMESPACE and
/// AW_SINDRI_TEMPORAL_TASK_QUEUE, each 'sindri' when unset. The worker, the clients and the activities all use them.
/// </summary>
public sealed class TemporalNamesTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact, Trait("Lane", "unit")]
    public void Both_are_sindri_when_unset()
    {
        var settings = TemporalSettings.FromEnvironment(static name => name == "AW_STATE_DIR" ? "/tmp/aw" : null);

        Assert.Equal(("sindri", "sindri"), (settings.Namespace, settings.TaskQueue));
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_is_read_from_its_variable_and_an_empty_one_is_the_default()
    {
        var named = TemporalSettings.FromEnvironment(static name => name switch
        {
            "AW_SINDRI_TEMPORAL_NAMESPACE" => "team-a",
            "AW_SINDRI_TEMPORAL_TASK_QUEUE" => "queue-b",
            _ => null,
        });
        var empty = TemporalSettings.FromEnvironment(static name => name.StartsWith("AW_SINDRI_TEMPORAL_", StringComparison.Ordinal) ? string.Empty : null);

        Assert.Equal(("team-a", "queue-b"), (named.Namespace, named.TaskQueue));
        Assert.Equal(("sindri", "sindri"), (empty.Namespace, empty.TaskQueue));
    }

    [Fact(Timeout = 120_000)]
    [Lane(TestLane.Integration)]
    public async Task An_instance_runs_on_a_non_default_namespace_and_task_queue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var server = await WorkflowsHarness.StartDevServer("team-a", ct);   // no 'sindri' namespace on this server
        await using var served = await Served.Start(_harness.Home, server, static name => name switch
        {
            "AW_SINDRI_TEMPORAL_NAMESPACE" => "team-a",
            "AW_SINDRI_TEMPORAL_TASK_QUEUE" => "queue-b",
            _ => null,
        }, TestContext.Current.CancellationToken);

        var started = await served.Cli("start", "workflow.step", "--item", "T-31", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        var open = await served.Until("T-31/step/1", static v => v.Task is not null);   // RunStep ran: only queue-b has a worker
        var described = await served.Handle("T-31/step/1").DescribeAsync(new() { Rpc = new() { CancellationToken = ct } });

        Assert.Equal((0, string.Empty), (started.Exit, started.Stderr));
        Assert.Equal("produce", open.State);
        Assert.Equal("queue-b", described.TaskQueue);
        Assert.Equal("team-a", server.Client.Options.Namespace);
    }
}
