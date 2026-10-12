using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.Workflows;
using Aw.Sindri.Tests.Workflows.Spike;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>
/// The desk wait's timeout on the time-skipping server (engine spec W20, §3.8; rev 8.1 §5.3, finding F1): the wait is
/// the state's timeout raised to 'workflow.deskTimeout' (2d), and its expiry parks with 'timeout' on the same run. A
/// 'then: retry' state never retries in desk mode. Lane decision (P4 unit (c)): W20 and §3.8 win over §5.3's
/// 'step.failed(timeout)'.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class DeskTimeoutTests(TimeSkippingServer server) : IClassFixture<TimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Desk_timeout_never_retries()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = worker.ExecuteAsync(stop.Token);
        var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-7", ct));
        var desk = new DeskInstance(sindri, handle);
        try
        {
            _ = await desk.Until(static v => v.Task is not null);
            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            var done = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));
            Assert.Equal("-> checks", done.Happened);   // 'checks': timeout { after: 30m, then: retry }

            await env.DelayAsync(TimeSpan.FromHours(1), ct);
            var waiting = await desk.Until(static v => v.Task is not null);
            await env.DelayAsync(TimeSpan.FromDays(2), ct);
            var parked = await desk.Until(static v => v.State == "parked");
            await env.DelayAsync(TimeSpan.FromDays(5), ct);
            var later = await desk.Until(static v => v.State == "parked");

            Assert.Equal(("checks", "2.1"), (waiting.State, waiting.Run?.Value));
            Assert.Equal((ParkReason.Timeout, "checks ran past 2d", "2.1"), (parked.Question?.Reason, parked.Question?.Question, parked.Run?.Value));
            Assert.Equal((ParkReason.Timeout, "2.1"), (later.Question?.Reason, later.Run?.Value));
            Assert.Equal(["workflow.started", "workflow.transition", "workflow.transition"], await desk.Events());
        }
        finally
        {
            await handle.TerminateAsync("test over");
            await stop.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
    }
}
