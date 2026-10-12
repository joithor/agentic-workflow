using System.Diagnostics;
using System.Globalization;
using Aw.Kernel.Testing;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows.Spike;

/// <summary>
/// Engine P0 pass line 2 on the time-skipping server: the three 'workflow.step' eval cases through the throwaway
/// interpreter in under 10 s in total, and a '4h' timeout firing in under a second of wall time.
/// </summary>
/// <param name="server">The time-skipping server.</param>
[Lane(TestLane.Integration)]
public sealed class SpikeEvalTests(TimeSkippingServer server) : IClassFixture<TimeSkippingServer>
{
    private const string Queue = "spike-evals";
    private static readonly string[] _cases = ["happy", "no-progress", "rounds"];
    private readonly WorkflowEnvironment _env = server.Environment;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact(Timeout = 30_000)]
    public async Task Eval_cases_reach_their_expected_states_in_under_ten_seconds()
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
                await RunCase(spike, name);
                timings.Add(string.Create(CultureInfo.InvariantCulture, $"{name} {one.Elapsed.TotalMilliseconds:F0} ms"));
            }
        }, ct);
        total.Stop();

        var line = string.Create(CultureInfo.InvariantCulture, $"evals: {string.Join(", ", timings)}; total {total.Elapsed.TotalMilliseconds:F0} ms");
        TestContext.Current.SendDiagnosticMessage(line);
        Assert.True(total.Elapsed < TimeSpan.FromSeconds(10), line);
    }

    [Fact(Timeout = 30_000)]
    public async Task Four_hour_timeout_parks_in_under_a_second_of_wall_time()
    {
        var ct = TestContext.Current.CancellationToken;
        var desk = new DeskTasks();
        var ledger = await SpikeLedger.InMemory(ct);
        var spike = new SpikeDesk(_env.Client, desk);
        const string id = "TIMEOUT-1/workflow.step/1";

        await SpikeWorker.Run(_env.Client, SpikeWorker.Options(Queue + "-timeout", desk, ledger), async () =>
        {
            var handle = await spike.Start(id, Queue + "-timeout", new SpikeRun("TIMEOUT-1", SpikeEvals.WorkflowStep));
            await spike.Opened(id, "1.1", ct);
            var wall = Stopwatch.StartNew();

            var view = await handle.GetResultAsync();
            wall.Stop();

            var line = string.Create(CultureInfo.InvariantCulture, $"4h timeout: {view.State}/{view.Park} after {wall.Elapsed.TotalMilliseconds:F0} ms wall");
            TestContext.Current.SendDiagnosticMessage(line);
            Assert.Equal(("parked", "timeout"), (view.State, view.Park));
            Assert.True(wall.Elapsed < TimeSpan.FromSeconds(1), line);
        }, ct);
    }

    private static async Task RunCase(SpikeDesk spike, string name)
    {
        var id = $"EVAL-{name}/workflow.step/1";
        var handle = await spike.Start(id, Queue, new SpikeRun("EVAL-1", SpikeEvals.WorkflowStep));
        var lines = SpikeEvals.Case(name);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            await spike.Complete(id, Run(i + 1), line.Report, Token);
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

    private static string Run(int entry) => string.Create(CultureInfo.InvariantCulture, $"{entry}.1");
}
