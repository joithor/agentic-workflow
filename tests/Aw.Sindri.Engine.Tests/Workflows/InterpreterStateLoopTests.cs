using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
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
/// 'Ring' from the workflow input); a wait state consumes the event the bridge delivered; a refused event parks the wait
/// and 'NeedsYou' flips; every history replays clean.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class InterpreterStateLoopTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
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

    /// <summary>A first input of 'fact:item' whose content was never stored: the repo read fails, the instance still starts, and 'Repo' stays unset (engine spec §5.1).</summary>
    [Fact(Timeout = 120_000)]
    public async Task A_fact_item_with_no_stored_content_starts_the_instance_and_leaves_Repo_unset()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        _harness.Recipe("workflow.item", WorkflowsHarness.Shipped("sindri/workflow.item.yaml"));
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var unstored = new FactValue("fact:sha256:" + new string('b', 64), new string('b', 64), HeadSha: null);
            var start = await WorkflowsHarness.RecipeStart(sindri, "workflow.item", "T-32", ct) with
            {
                Inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:item")] = new InputFact("item.json", unstored) },
            };
            var handle = await WorkflowsHarness.Start(env.Client, start);
            var started = await WorkflowsHarness.Until(handle, static v => v.State == "triaged");

            var attributes = await Attributes(handle, static a => a.ContainsKey(SindriTemporal.StateKey));
            Assert.Equal("triaged", started.State);
            Assert.False(attributes.ContainsKey(SindriTemporal.RepoKey));
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact(Timeout = 120_000)]
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
            var result = await handle.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            var after = await Attributes(handle, static a => a.Get(SindriTemporal.StateKey) == "dropped");

            Assert.Null(waiting.Task);
            Assert.Equal(("triaged", false, "agentic-workflow"), (before.Get(SindriTemporal.StateKey), before.Get(SindriTemporal.NeedsYouKey), before.Get(SindriTemporal.RepoKey)));
            Assert.Equal(("dropped", "unresolved"), (result.State, result.Outcome));
            Assert.Equal((false, "agentic-workflow"), (after.Get(SindriTemporal.NeedsYouKey), after.Get(SindriTemporal.RepoKey)));
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
        }, ct);
    }

    /// <summary>
    /// 'item.routed' with outcome startable matches triaged -> startable, whose gate (here one with no runtime; the shipped one has had its own since engine P7)
    /// so it refuses (fail closed, W5) and the wait parks with needs-answer (refusal precedence, §5.8): NeedsYou flips,
    /// and the ledger has the start and the refusal, one row each. This 'fact:item' names no repo, so 'Repo' stays unset.
    /// </summary>
    [Fact(Timeout = 120_000)]
    public async Task A_refused_event_parks_the_wait_and_needs_you_flips_to_true()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        _harness.Recipe("workflow.item", WorkflowsHarness.Shipped("sindri/workflow.item.yaml").Replace("version: 3", "version: 4", StringComparison.Ordinal).Replace("gates: [gate.route.startable]", "gates: [gate.test.check]", StringComparison.Ordinal));   // a gate with no runtime refuses (the shipped gate has one since engine P7)
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var item = _harness.Fact("""{"id":"T-41","spec":"Add a --version flag."}""");
            var start = await WorkflowsHarness.RecipeStart(sindri, "workflow.item", "T-41", ct) with
            {
                Inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:item")] = new InputFact("item.json", item) },
            };
            var handle = await WorkflowsHarness.Start(env.Client, start);
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "triaged");
            var waiting = await Attributes(handle, static a => a.ContainsKey(SindriTemporal.StateKey));

            var routed = new WaitSatisfied("service:workflow.events", new EventWaitSource(EventKind.From("item.routed"), 1), OutcomeName.From("startable"), new Dictionary<FactKey, FactValue>());
            await handle.SignalAsync(wf => wf.EventAsync(routed));
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            var flipped = await Attributes(handle, static a => a.Get(SindriTemporal.StateKey) == "parked");
            var rows = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 100, ct)).Value
                .Where(static e => e.Envelope.Type.StartsWith("workflow.", StringComparison.Ordinal)).ToList();

            Assert.Equal(("triaged", false), (waiting.Get(SindriTemporal.StateKey), waiting.Get(SindriTemporal.NeedsYouKey)));
            Assert.Equal(ParkReason.NeedsAnswer, parked.Question?.Reason);
            Assert.True(flipped.Get(SindriTemporal.NeedsYouKey));
            Assert.Equal(["workflow.started", "workflow.refused"], [.. rows.Select(static e => e.Envelope.Type)]);
            Assert.Equal(2, rows.Select(static e => e.IdempotencyKey).Distinct(StringComparer.Ordinal).Count());
            Assert.False(flipped.ContainsKey(SindriTemporal.RepoKey));
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_cancel_sent_while_the_desk_task_is_open_ends_the_instance_without_waiting_for_the_report()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-51", ct));
            var open = await WorkflowsHarness.Until(handle, static v => v.Task is not null);
            var nonce = CommandNonce.From(new string('d', 32));
            var cancel = new CancelRequested("human:cli", CancelOrigin.Command, new CommandStamp(nonce, new string('0', 64)), Sequence: null, Parent: null);

            // why: the desk wait (unit (c)) must wake for an Update unit (a)'s loop takes, or a cancel waits out the desk timeout
            var result = await handle.ExecuteUpdateAsync(wf => wf.CancelAsync(cancel), new WorkflowUpdateOptions(nonce.Value)).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            var ended = await handle.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            var rows = (await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 100, ct)).Value
                .Where(static e => e.Envelope.Type.StartsWith("workflow.", StringComparison.Ordinal)).ToList();

            Assert.Equal(("produce", "1.1"), (open.State, open.Run?.Value));
            Assert.Equal("cancelled", result.Instance.State);
            Assert.Equal("cancelled", ended.State);
            Assert.Equal(["workflow.started", "workflow.transition"], [.. rows.Select(static e => e.Envelope.Type)]);
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
        }, ct);
    }

    /// <summary>The instance's search attributes, described until <paramref name="holds"/> (visibility follows the workflow task).</summary>
    private static async Task<SearchAttributeCollection> Attributes(WorkflowHandle handle, Func<SearchAttributeCollection, bool> holds)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        var last = SearchAttributeCollection.Empty;
        while (RealTime.NowDateTime < deadline)
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
