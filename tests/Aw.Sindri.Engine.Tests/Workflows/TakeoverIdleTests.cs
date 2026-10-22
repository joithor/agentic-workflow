using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The takeover's idle reminder on the time-skipping server (engine spec §5.5; verification plan journey 8): two hours
/// after a takeover it records one 'workflow.notified' row (P6 delivers it through 'notify.desk') and clears; it never
/// hands back, and nothing more is recorded however long the takeover lasts. The state timer stays paused throughout.
/// After a handback the idle timer is gone, so nothing is recorded.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TakeoverIdleTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task TakeoverIdle_records_one_notified_row_and_clears_and_never_hands_back()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var env = server.Environment;
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-20", ct));
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            _ = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(new TakeoverRequested("human:cli", Stamp('1'))), new WorkflowUpdateOptions(new string('1', 32)));

            await env.DelayAsync(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(1), ct);
            var once = await Notified(sindri, "T-20/dev-timeout/1", atLeast: 1, ct);
            await env.DelayAsync(TimeSpan.FromHours(5), ct);
            var later = await WorkflowsHarness.Until(handle, static v => v.State == "taken-over");
            // An Update completes after the workflow tasks that are pending, so the count after it is the final one.
            _ = await handle.ExecuteUpdateAsync(wf => wf.HandbackAsync(new HandbackRequested("human:cli", Stamp('2'))), new WorkflowUpdateOptions(new string('2', 32)));
            var still = await Notified(sindri, "T-20/dev-timeout/1", atLeast: 1, ct);

            Assert.Equal((1, 1), (once, still));
            Assert.Equal(("taken-over", null), (later.State, later.Task));
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task TakeoverIdle_after_a_handback_records_nothing()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var env = server.Environment;
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-21", ct));
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            _ = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(new TakeoverRequested("human:cli", Stamp('1'))), new WorkflowUpdateOptions(new string('1', 32)));
            await env.DelayAsync(TimeSpan.FromHours(1), ct);
            _ = await handle.ExecuteUpdateAsync(wf => wf.HandbackAsync(new HandbackRequested("human:cli", Stamp('2'))), new WorkflowUpdateOptions(new string('2', 32)));

            await env.DelayAsync(TimeSpan.FromHours(3), ct);   // the 2 h idle timer would have run out by now
            // A second takeover is an Update, so it completes after any workflow task the skip left pending.
            _ = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(new TakeoverRequested("human:cli", Stamp('3'))), new WorkflowUpdateOptions(new string('3', 32)));
            var notified = await Notified(sindri, "T-21/dev-timeout/1", atLeast: 0, ct);

            Assert.Equal(0, notified);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    /// <summary>The instance's 'takeoverIdle' 'workflow.notified' rows, once there are at least <paramref name="atLeast"/> (the mirror is a local activity).</summary>
    private static async Task<int> Notified(IServiceProvider sindri, string address, int atLeast, CancellationToken ct)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        while (true)
        {
            var read = await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, ct);
            Assert.False(read.IsError, read.IsError ? read.Error.Message : null);
            var count = read.Value.Count(e => e.Envelope.Type == "workflow.notified"
                && e.Envelope.Payload.Contains($"\"{address}\"", StringComparison.Ordinal)
                && e.Envelope.Payload.Contains("takeoverIdle", StringComparison.Ordinal));
            if (count >= atLeast || RealTime.NowDateTime >= deadline)
            {
                return count;
            }

            await Task.Delay(100, ct);
        }
    }

    private static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('0', 64));
}
