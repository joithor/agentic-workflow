using System.Diagnostics;
using System.Globalization;
using Aw.Kernel.Testing;
using Temporalio.Client;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// Engine P4a acceptance check 2 on the time-skipping server: the three 'workflow.step' eval cases through the test
/// interpreter in under 10 s in total, and the '4h' timeout. The time-skipping server doesn't skip time while any
/// activity is scheduled or open, so a desk Step held open as an async-completion activity (rev 8 §5.3) blocks its own
/// timeout; with the report as the 'complete' Update (rev 8 §5.2) and no open activity, the 4h timeout fires at once.
/// </summary>
/// <param name="server">The time-skipping server.</param>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]   // wall clock (it asserts wall clock bounds of 1 s and 10 s)
public sealed class SpikeEvalTests(TimeSkippingServer server) : IClassFixture<TimeSkippingServer>
{
    private const string Queue = "spike-evals";
    private static readonly string[] _cases = ["happy", "no-progress", "rounds"];
    private readonly WorkflowEnvironment _env = server.Environment;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory(Timeout = 30_000)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Eval_cases_reach_their_expected_states_in_under_ten_seconds(bool deskByUpdate)
    {
        var ct = TestContext.Current.CancellationToken;
        var desk = new DeskTasks();
        var ledger = await SpikeLedger.InMemory(ct);
        var spike = new SpikeDesk(_env.Client, desk);
        var timings = new List<string>();
        var total = Stopwatch.StartNew();

        await SpikeWorker.Run(_env.Client, SpikeWorker.Options(Queue, desk, ledger), async () =>
        {
            foreach (var name in _cases)
            {
                var one = Stopwatch.StartNew();
                await RunCase(spike, name, deskByUpdate);
                timings.Add(string.Create(CultureInfo.InvariantCulture, $"{name} {one.Elapsed.TotalMilliseconds:F0} ms"));
            }
        }, ct);
        total.Stop();

        var line = string.Create(CultureInfo.InvariantCulture,
            $"evals (desk {(deskByUpdate ? "Update" : "async-completion activity")}): {string.Join(", ", timings)}; total {total.Elapsed.TotalMilliseconds:F0} ms");
        SpikeLog.Write(line);
        Assert.True(total.Elapsed < TimeSpan.FromSeconds(10), line);
    }

    [Fact(Timeout = 30_000)]
    public async Task Four_hour_timeout_parks_in_under_a_second_when_the_desk_step_is_an_update_wait()
    {
        var ct = TestContext.Current.CancellationToken;

        // why: a server of its own (finding F1). Time skipping on the shared fixture depends on what the other tests in
        // the class left behind: an async-completion desk activity scheduled there by an earlier test keeps it off, and
        // the 4h timer then waits in real time until the 30 s test timeout.
        await using var env = await TemporalServerFixture.Start(ServerKind.TimeSkipping);
        var (spike, options) = await Worker(env.Client, "timeout-update", ct);
        const string id = "TIMEOUT-U/workflow.step/1";

        await SpikeWorker.Run(env.Client, options, async () =>
        {
            var handle = await spike.Start(id, options.TaskQueue!, new SpikeRun("TIMEOUT-U", SpikeEvals.WorkflowStep, DeskByUpdate: true));
            await spike.Opened(id, "1.1", ct);
            var probe = Stopwatch.StartNew();
            while (!(await handle.FetchHistoryAsync()).Events.Any(e => e.EventType == Temporalio.Api.Enums.V1.EventType.TimerStarted) && probe.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(50, ct);
            }

            SpikeLog.Write(string.Create(CultureInfo.InvariantCulture, $"4h timeout (desk Update): timer in history {probe.ElapsedMilliseconds} ms after the desk task opened; the result is awaited only then (a wait begun while an activity was open never resumed skipping: 30 s test timeout)"));
            var wall = Stopwatch.StartNew();

            var view = await handle.GetResultAsync();
            wall.Stop();

            var line = string.Create(CultureInfo.InvariantCulture, $"4h timeout (desk Update): {view.State}/{view.Park} after {wall.Elapsed.TotalMilliseconds:F0} ms wall");
            SpikeLog.Write(line);
            Assert.Equal(("parked", "timeout"), (view.State, view.Park));
            Assert.True(wall.Elapsed < TimeSpan.FromSeconds(1), line);
        }, ct);
    }

    [Fact(Timeout = 30_000)]
    public async Task Four_hour_timeout_does_not_skip_while_the_desk_activity_is_open()
    {
        var ct = TestContext.Current.CancellationToken;

        // why: a server of its own. The activity this test leaves open keeps time skipping off for the server's life
        // (finding F1), so on the shared fixture it would stall the Update-timeout test whenever that runs after it.
        await using var env = await TemporalServerFixture.Start(ServerKind.TimeSkipping);
        var (spike, options) = await Worker(env.Client, "timeout-activity", ct);
        const string id = "TIMEOUT-A/workflow.step/1";

        await SpikeWorker.Run(env.Client, options, async () =>
        {
            var handle = await spike.Start(id, options.TaskQueue!, new SpikeRun("TIMEOUT-A", SpikeEvals.WorkflowStep));
            await spike.Opened(id, "1.1", ct);
            var wall = Stopwatch.StartNew();

            _ = await Assert.ThrowsAsync<TimeoutException>(() => handle.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(2), ct));
            var view = await handle.QueryAsync(w => w.View);
            SpikeLog.Write(string.Create(CultureInfo.InvariantCulture,
                $"4h timeout (desk activity open): still {view.State} run {view.Run} after {wall.Elapsed.TotalMilliseconds:F0} ms wall; no time skipped"));
            Assert.Equal(("produce", "1.1"), (view.State, view.Run));
            await handle.TerminateAsync("spike: documented unsupported");
        }, ct);
    }

    private static async Task<(SpikeDesk Spike, Temporalio.Worker.TemporalWorkerOptions Options)> Worker(ITemporalClient client, string queue, CancellationToken ct)
    {
        var desk = new DeskTasks();
        var ledger = await SpikeLedger.InMemory(ct);
        return (new SpikeDesk(client, desk), SpikeWorker.Options(queue, desk, ledger));
    }

    private static async Task RunCase(SpikeDesk spike, string name, bool deskByUpdate)
    {
        var id = deskByUpdate ? $"EVAL-{name}/workflow.step/2" : $"EVAL-{name}/workflow.step/1";
        var handle = await spike.Start(id, Queue, new SpikeRun("EVAL-1", SpikeEvals.WorkflowStep, deskByUpdate));
        var lines = SpikeEvals.Case(name);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            await Report(spike, handle, id, i + 1, line.Report, deskByUpdate);
            var ends = line.State == "parked" || SpikeEvals.WorkflowStep.State(line.State).Terminal;
            if (!ends)
            {
                await spike.Opened(id, Run(i + 2), Token);
            }

            var view = ends ? await handle.GetResultAsync() : await handle.QueryAsync(w => w.View);
            Assert.True((line.State, line.Park) == (view.State, view.Park), string.Create(CultureInfo.InvariantCulture,
                $"{name} fail at evals/workflow.step/{name}.jsonl:{line.Number}: expected {line.State}/{line.Park}, got {view.State}/{view.Park}"));
        }
    }

    private static async Task Report(SpikeDesk spike, WorkflowHandle<SpikeStepWorkflow, SpikeView> handle, string id, int entry, SpikeReport report, bool deskByUpdate)
    {
        if (!deskByUpdate)
        {
            await spike.Complete(id, Run(entry), report, Token);
            return;
        }

        await spike.Opened(id, Run(entry), Token);
        var nonce = $"{id}:{Run(entry)}";
        Assert.Equal(Run(entry), await SpikeDesk.CompleteByUpdate(handle, Run(entry), report, nonce));
        Assert.Equal(Run(entry), await SpikeDesk.CompleteByUpdate(handle, Run(entry), report, nonce));
    }

    private static string Run(int entry) => string.Create(CultureInfo.InvariantCulture, $"{entry}.1");
}
