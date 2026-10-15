using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Workflows;
using Aw.Sindri.Tests.Workflows.Spike;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.OperatorService.V1;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// 'workflow answer --resume' on a desk timeout park (engine spec §3.8, §5.8 'rearm'; rev 8.1 §5.3) on the
/// time-skipping server: the resume ends the park on the same run and re-arms the desk deadline from the answer, so the
/// old, passed deadline doesn't park it again at once.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class ResumeDeadlineTests(TimeSkippingServer server) : IClassFixture<TimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Resume_on_a_timeout_park_rearms_the_desk_deadline()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var attributes = new AddSearchAttributesRequest { Namespace = env.Client.Options.Namespace };
        attributes.SearchAttributes.Add(SindriTemporal.SearchAttributes.ToDictionary(static a => a.Name, static a => a.Type, StringComparer.Ordinal));
        _ = await env.Client.OperatorService.AddSearchAttributesAsync(attributes);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var running = worker.ExecuteAsync(stop.Token);
        var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-19", ct));
        var desk = new DeskInstance(sindri, handle);
        try
        {
            _ = await desk.Until(static v => v.Task is not null);
            await env.DelayAsync(TimeSpan.FromDays(3), ct);
            var parked = await desk.Until(static v => v.State == "parked");
            var resume = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('a', 32)), new string('b', 64)), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);

            var resumed = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(resume), new WorkflowUpdateOptions(new string('a', 32))).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await env.DelayAsync(TimeSpan.FromDays(1), ct);
            var waiting = await desk.Until(static v => v.Task is not null);
            await env.DelayAsync(TimeSpan.FromDays(2), ct);
            var again = await desk.Until(static v => v.State == "parked");

            Assert.Equal((ParkReason.Timeout, "1.1"), (parked.Question?.Reason, parked.Run?.Value));
            Assert.Equal(("produce", "resumed (rearm): -> produce"), (resumed.Instance.State, resumed.Happened));
            Assert.Equal(("produce", "1.1", null), (waiting.State, waiting.Run?.Value, waiting.Question));
            Assert.Equal((ParkReason.Timeout, "1.1"), (again.Question?.Reason, again.Run?.Value));
        }
        finally
        {
            await handle.TerminateAsync("test over");
            await stop.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        }
    }
}
