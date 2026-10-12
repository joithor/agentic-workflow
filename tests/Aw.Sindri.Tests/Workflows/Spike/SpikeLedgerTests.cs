using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Api.Enums.V1;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// Engine P4a acceptance check 3: the ledger holds one 'workflow.transition' per transition after a replay. The interpreter
/// mirrors through the real 'RecordEvent' into S1's SQLite ledger (ILedgerStore); the replay is forced three ways:
/// a new worker with no sticky cache (every workflow task replays the whole history), the SDK's WorkflowReplayer over
/// the fetched history, and a direct re-execution of a recorded event (the at-least-once retry a crash can cause).
/// </summary>
/// <param name="server">The dev server (the time-skipping server keeps a task on a dead worker's sticky queue, see TemporalProbes).</param>
[Lane(TestLane.Integration)]
public sealed class SpikeLedgerTests(LocalServer server) : IClassFixture<LocalServer>, IDisposable
{
    private const string Queue = "spike-ledger";
    private const string Id = "LEDGER-1/workflow.step/1";
    private static readonly Dictionary<string, string> _none = new(StringComparer.Ordinal);
    private readonly WorkflowEnvironment _env = server.Environment;
    private readonly TempHome _home = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    /// <summary>
    /// Engine spec rev 8.1 §5.6: the key is 'workflow:{workflow id}:{run id}:{activity id}', the RecordEvent local
    /// activity's own deterministic ActivityInfo.ActivityId (the same on every replay), not a workflow-side counter.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Replay_leaves_one_ledger_row_per_transition_keyed_by_the_activity_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var probe = new FakeProcessProbe();
        var ledger = await SpikeLedger.Open(SqliteStores.Create(_home.Path, clock, probe), clock, probe, ct);
        var desk = new DeskTasks();
        var spike = new SpikeDesk(_env.Client, desk);
        var (handle, before) = await TwoTransitionsThenWorkerExits(ledger, desk, spike);

        await SpikeWorker.Run(_env.Client, SpikeWorker.Options(Queue, desk, ledger, maxCachedWorkflows: 0), async () =>
        {
            Assert.Equal("verify", (await handle.QueryAsync(w => w.View)).State);
            await spike.Complete(Id, "3.1", new SpikeReport("accepted", _none), ct);
            Assert.Equal("done", (await handle.GetResultAsync()).State);
        }, ct);
        var history = await handle.FetchHistoryAsync();
        var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<SpikeStepWorkflow>()).ReplayWorkflowAsync(history, throwOnReplayFailure: false, ct);
        var runId = history.Events.First().WorkflowExecutionStartedEventAttributes.OriginalExecutionRunId;
        var recorded = ledger.Recorder.Log.Where(l => l.EndsWith(":new", StringComparison.Ordinal)).Select(l => l[..^":new".Length]).ToList();
        var again = new ActivityEnvironment { Info = ActivityEnvironment.DefaultInfo with { WorkflowId = Id, WorkflowRunId = runId, ActivityId = recorded[1], IsLocal = true } };
        var retried = await again.RunAsync(() => ledger.Recorder.RecordAsync(new WorkflowEventRecord(Id, "workflow.transition", "LEDGER-1", "produce", "checks", "step.completed", "done", "1.1")));

        var transitions = await ledger.Read("workflow.transition", ct);
        var started = await ledger.Read("workflow.started", ct);
        var markers = history.Events.Count(e => e.EventType == EventType.MarkerRecorded);
        var line = string.Create(CultureInfo.InvariantCulture,
            $"ledger: transitions before replay {before}, after {transitions.Count}; started {started.Count}; RecordEvent executions {ledger.Recorder.Executions} [{string.Join(' ', ledger.Recorder.Log)}]; local-activity markers in history {markers}; db {_home.Path}");
        SpikeLog.Write(line);
        Keep();

        Assert.Null(replayed.ReplayFailure);
        Assert.True(retried.AlreadyRecorded, line);
        Assert.Equal((2, 3, 1, 4), (before, transitions.Count, started.Count, markers));
        Assert.Equal(4, recorded.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["produce>checks", "checks>verify", "verify>done"], [.. transitions.Select(t => Payload(t.Envelope.Payload))]);
        Assert.Equal(
            [.. recorded.Skip(1).Select(activityId => $"workflow:{Id}:{runId}:{activityId}")],
            [.. transitions.Select(t => t.IdempotencyKey)]);
    }

    /// <summary>Worker A (sticky cache on) takes the instance produce -> checks -> verify, then exits.</summary>
    private async Task<(Temporalio.Client.WorkflowHandle<SpikeStepWorkflow, SpikeView> Handle, int Before)> TwoTransitionsThenWorkerExits(SpikeLedger ledger, DeskTasks desk, SpikeDesk spike)
    {
        Temporalio.Client.WorkflowHandle<SpikeStepWorkflow, SpikeView>? handle = null;
        await SpikeWorker.Run(_env.Client, SpikeWorker.Options(Queue, desk, ledger), async () =>
        {
            handle = await spike.Start(Id, Queue, new SpikeRun("LEDGER-1", SpikeEvals.WorkflowStep));
            await spike.Complete(Id, "1.1", new SpikeReport("done", new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:output"] = "o" }), Token);
            await spike.Complete(Id, "2.1", new SpikeReport("passed", _none), Token);
            await spike.Opened(Id, "3.1", Token);
        }, Token);
        return (handle!, (await ledger.Read("workflow.transition", Token)).Count);
    }

    private static string Payload(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.GetProperty("from").GetString() + ">" + document.RootElement.GetProperty("to").GetString();
    }

    /// <summary>AW_SPIKE_LEDGER_COPY=&lt;dir&gt; keeps a copy of ledger.db (and its WAL) for counting with sqlite3.</summary>
    private void Keep()
    {
        if (Environment.GetEnvironmentVariable("AW_SPIKE_LEDGER_COPY") is not { Length: > 0 } target)
        {
            return;
        }

        _ = Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(_home.Path, "ledger.db*"))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
        }
    }
}
