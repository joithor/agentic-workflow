using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Common;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The interpreter's state loop on the time-skipping server (engine spec rev 8 §5.1, §5.4, §5.5, §5.8): the search
/// attributes are set on start and on every transition ('Repo' from 'fact:item''s repo, unset otherwise; 'Tenant' and
/// 'Ring' from the workflow input); a wait state consumes the event the bridge delivered; a wait's timeout parks it and
/// 'NeedsYou' flips; every history replays clean.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class InterpreterStateLoopTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    /// <summary>A wait for 'item.routed' that parks after 1h: NeedsYou is false while it waits and true once it parks.</summary>
    private static readonly string _waitPark = string.Join('\n',
    [
        "schema: 1",
        "id: workflow.wait-park",
        "version: 1",
        "initial: routed",
        "states:",
        "  routed: { kind: wait, waitFor: { event: item.routed }, timeout: { after: 1h, then: park },",
        "            next: \"Waiting for the router to route {item}.\" }",
        "  done:   { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: routed, to: done, on: wait.satisfied, when: { outcome: startable } }",
    ]) + "\n";

    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Start_sets_the_search_attributes_from_the_input_with_no_repo_without_fact_item()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var start = await WorkflowsHarness.StepStart(sindri, "T-21", ct) with { Tenant = "acme", Ring = 2 };
            var handle = await WorkflowsHarness.Start(env.Client, start);
            _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

            var attributes = await Attributes(handle, static a => a.ContainsKey(SindriTemporal.StateKey));
            Assert.Equal(
                ("T-21", "workflow.step", "produce", true, "acme", "2"),
                (attributes.Get(SindriTemporal.ItemKey), attributes.Get(SindriTemporal.RecipeKey), attributes.Get(SindriTemporal.StateKey),
                 attributes.Get(SindriTemporal.NeedsYouKey), attributes.Get(SindriTemporal.TenantKey), attributes.Get(SindriTemporal.RingKey)));
            Assert.False(attributes.ContainsKey(SindriTemporal.RepoKey));
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task A_delivered_event_moves_the_waiting_instance_and_repo_comes_from_fact_item()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        _harness.Recipe("workflow.item", WorkflowsHarness.Shipped("sindri/workflow.item.yaml"));
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var item = _harness.Fact("""{"id":"T-31","repo":"agentic-workflow","spec":"Add a --version flag."}""");
            var start = await WorkflowsHarness.RecipeStart(sindri, "workflow.item", "T-31", ct) with
            {
                Inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:item")] = new InputFact("item.json", item) },
            };
            var handle = await WorkflowsHarness.Start(env.Client, start);
            var waiting = await WorkflowsHarness.Until(handle, static v => v.State == "triaged");
            var before = await Attributes(handle, static a => a.ContainsKey(SindriTemporal.StateKey));

            var routed = new WaitSatisfied("service:workflow.events", new EventWaitSource(EventKind.From("item.routed"), 1), OutcomeName.From("notStartable"), new Dictionary<FactKey, FactValue>());
            await handle.SignalAsync(wf => wf.EventAsync(routed));
            var result = await handle.GetResultAsync();
            var after = await Attributes(handle, static a => a.Get(SindriTemporal.StateKey) == "dropped");

            Assert.Null(waiting.Task);
            Assert.Equal(("triaged", false, "agentic-workflow"), (before.Get(SindriTemporal.StateKey), before.Get(SindriTemporal.NeedsYouKey), before.Get(SindriTemporal.RepoKey)));
            Assert.Equal(("dropped", "unresolved"), (result.State, result.Outcome));
            Assert.Equal((false, "agentic-workflow"), (after.Get(SindriTemporal.NeedsYouKey), after.Get(SindriTemporal.RepoKey)));
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
        }, ct);
    }

    [Fact]
    public async Task A_wait_that_times_out_parks_and_needs_you_flips_to_true()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        _harness.Recipe("workflow.wait-park", _waitPark);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.wait-park", "T-41", ct));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "routed");
            var waiting = await Attributes(handle, static a => a.ContainsKey(SindriTemporal.StateKey));

            await env.DelayAsync(TimeSpan.FromHours(2), ct);
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            var flipped = await Attributes(handle, static a => a.Get(SindriTemporal.StateKey) == "parked");

            Assert.Equal(("routed", false), (waiting.Get(SindriTemporal.StateKey), waiting.Get(SindriTemporal.NeedsYouKey)));
            Assert.Equal(ParkReason.Timeout, parked.Question?.Reason);
            Assert.True(flipped.Get(SindriTemporal.NeedsYouKey));
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    /// <summary>The instance's search attributes, described until <paramref name="holds"/> (visibility follows the workflow task).</summary>
    private static async Task<SearchAttributeCollection> Attributes(WorkflowHandle handle, Func<SearchAttributeCollection, bool> holds)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        var last = SearchAttributeCollection.Empty;
        while (DateTime.UtcNow < deadline)
        {
            last = (await handle.DescribeAsync()).TypedSearchAttributes;
            if (holds(last))
            {
                return last;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"the search attributes never got there; last seen {string.Join(", ", last.UntypedValues.Select(static p => $"{p.Key.Name}={p.Value}"))}");
        return last;
    }
}
