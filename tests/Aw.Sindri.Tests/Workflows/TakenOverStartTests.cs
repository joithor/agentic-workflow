using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// An instance started taken over (UI spec E-19) whose first entry parked at once (SND-WF-010): the handback returns to
/// that park, so no state timer was paused and status has no 'paused timeout' to show (engine P5 review).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TakenOverStartTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private const string Recipe = "schema: 1\nid: workflow.no-runtime\nversion: 1\ninputs: [{ key: fact:item, type: item, kind: item }]\ninitial: hold\n"
        + "states:\n"
        + "  hold: { kind: active, step: step.nowhere, timeout: { after: 1h, then: park }, next: \"No runtime here.\" }\n"
        + "  done: { kind: terminal, outcome: resolved }\n"
        + "transitions:\n"
        + "  - { from: hold, to: done, on: step.completed, when: { outcome: done } }\n";

    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A_start_taken_over_whose_first_entry_parked_at_once_shows_no_paused_timeout()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.no-runtime", Recipe);
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.RecipeStart(sindri, "workflow.no-runtime", "T-33", ct);
            var handle = await WorkflowsHarness.Start(server.Environment.Client, start with { OpenLaunch = "LCH-0123456789abcdef0123456789abcdef" });
            var view = await WorkflowsHarness.Until(handle, static v => v.State == EngineStates.TakenOver);

            Assert.Null(view.Paused);
            await handle.TerminateAsync("test over");
        }, ct);
    }
}
