using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The heavy resource on the time-skipping server (line spec LN-WF3, §8.4) over the real directory lock with one slot: a 'waitFor: { resource: heavy }'
/// takes the slot, the instance holds it while it runs Steps off the desk, and gives it up at a desk Step, when it ends or is cancelled; a Step after
/// a desk Step, or after a worker restart, takes one first; a wait that runs out parks.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HeavySlotTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private static readonly BlockId _judge = BlockId.From("step.judge.choose");
    private static int _items = 100;
    private readonly WorkflowsHarness _harness = new();
    private readonly string _lock = Path.Combine(Directory.CreateTempSubdirectory("aw-heavy-wf-").FullName, "heavy-job.lock");

    public void Dispose()
    {
        _harness.Dispose();
        Directory.Delete(Path.GetDirectoryName(_lock)!, recursive: true);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_second_instance_waits_for_the_slot_until_the_first_gives_it_up_and_a_Step_knows_it_holds_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = FakeBlockStep.Gated(gate.Task, "ui");
        await Run(fake, async rig =>
        {
            var first = await rig.Start(Next());
            _ = await WorkflowsHarness.Until(first, static v => v.State == "first");
            await Eventually(() => fake.Runs == 1 && Directory.Exists(_lock), ct);
            var second = await rig.Start(Next());
            _ = await WorkflowsHarness.Until(second, static v => v.State == "slot");
            await Task.Delay(TimeSpan.FromSeconds(2), ct);   // the lock's poll is one second: a second instance that could have taken the slot would have

            Assert.Equal((1, "slot"), (fake.Runs, (await WorkflowsHarness.Until(second, static _ => true)).State));
            Assert.True(fake.Last!.HeavySlotHeld);

            gate.SetResult();   // the first runs its Steps to the desk and gives the slot up; the second takes it
            _ = await WorkflowsHarness.Until(first, static v => v.State == "desk");
            _ = await WorkflowsHarness.Until(second, static v => v.State == "desk");
            Assert.Equal(4, fake.Runs);
            Assert.False(Directory.Exists(_lock));
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task The_slot_is_given_up_at_a_desk_Step_and_taken_again_before_the_next_Step()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runs = 0;
        var fake = new FakeBlockStep(async (_, token) =>
        {
            if (Interlocked.Increment(ref runs) > 2)
            {
                await gate.Task.WaitAsync(token);   // the first two runs finish at once; the third (after the desk) waits
            }

            return FakeResult();
        });
        await Run(fake, async rig =>
        {
            var handle = await rig.Start(Next());
            var desk = await WorkflowsHarness.Until(handle, static v => v.State == "desk" && v.Run is not null);
            Assert.False(Directory.Exists(_lock));   // the desk Step does not hold the box's slot

            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(DeskReport(desk.Run!.Value)), new WorkflowUpdateOptions(new string('d', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "third");
            await Eventually(() => fake.Runs == 3 && Directory.Exists(_lock), ct);   // taken again before the Step ran
            Assert.True(fake.Last!.HeavySlotHeld);

            gate.SetResult();
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "done");
            await Eventually(() => !Directory.Exists(_lock), ct);
            Assert.Equal(["heavy.held", "heavy.released", "heavy.held", "heavy.released"], await Rows(rig.Sindri, ct));
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Cancelling_an_instance_that_holds_the_slot_releases_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = FakeBlockStep.Hanging();
        await Run(fake, async rig =>
        {
            var handle = await rig.Start(Next());
            await Eventually(() => fake.Runs == 1 && Directory.Exists(_lock), ct);

            _ = await ChildTree.Cancel(handle);

            await Eventually(() => !Directory.Exists(_lock), ct);
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_wait_that_runs_out_parks_and_an_answer_resumes_it_into_the_slot_once_it_is_free()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = FakeBlockStep.Completing("ui");
        await Run(fake, async rig =>
        {
            var blocker = await rig.Sindri.GetRequiredService<IHeavySlots>().Take("test blocker", TimeSpan.FromSeconds(5), waiting: null, ct);
            var handle = await rig.Start(Next());
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "slot");

            await WorkflowsHarness.Skip(server.Environment, TimeSpan.FromHours(25), ct);
            var parked = await WorkflowsHarness.Until(handle, static v => v.State == "parked");
            Assert.Equal(ParkReason.Timeout, parked.Question?.Reason);
            Assert.Equal(0, fake.Runs);

            await blocker.Value.Lease!.DisposeAsync();
            _ = await ChildTree.Resume(handle);
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "desk");
            Assert.Equal(2, fake.Runs);
        });
    }

    [Fact(Timeout = 180_000)]
    public async Task After_a_worker_restart_the_next_Step_takes_a_slot_before_it_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stuck = FakeBlockStep.Hanging();
        WorkflowHandle<RecipeWorkflow, RecipeResult>? handle = null;
        await Run(stuck, async rig =>
        {
            handle = await rig.Start(Next());
            await Eventually(() => stuck.Runs == 1 && Directory.Exists(_lock), ct);
        });   // the worker and its registry are gone with the host: the lease is lost, the instance is still in its heavy phase

        var next = FakeBlockStep.Gated(gate.Task, "ui");
        await Run(next, async rig =>
        {
            await WorkflowsHarness.Skip(server.Environment, TimeSpan.FromMinutes(1), ct);   // the run that died with the old worker times out on its heartbeat
            _ = await WorkflowsHarness.Until(handle!, static v => v.State == "second");
            await Eventually(() => next.Runs == 1 && Directory.Exists(_lock), ct);   // taken again before 'second' ran

            Assert.True(next.Last!.HeavySlotHeld);
            gate.SetResult();
            _ = await WorkflowsHarness.Until(handle!, static v => v.State == "desk");
        }, start: false);
    }

    private static string Next() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"T-{Interlocked.Increment(ref _items)}");

    private static StepCompleted DeskReport(RunToken run) => new(
        "human:cli", run, OutcomeName.From("done"),
        new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null) },
        Tokens: 0, StepRuntime.Desk, new string('f', 64));

    private static BlockStepResult FakeResult() => BlockStepResult.Completed(
        OutcomeName.From("ui"),
        new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:decision")] = JsonSerializer.SerializeToElement(new { choice = "ui", provider = "fake", decisionId = "JD-1" }) });

    /// <summary>The 'workflow.heavy' rows' triggers, in ledger order.</summary>
    private static async Task<IReadOnlyList<string>> Rows(IServiceProvider sindri, CancellationToken ct)
    {
        var read = await sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, ct);
        Assert.False(read.IsError, read.IsError ? read.Error.Message : null);
        return [.. read.Value.Where(static e => e.Envelope.Type == "workflow.heavy").Select(static e => JsonDocument.Parse(e.Envelope.Payload).RootElement.GetProperty("trigger").GetString()!)];
    }

    private static async Task Eventually(Func<bool> condition, CancellationToken ct)
    {
        var deadline = RealTime.NowDateTime.AddSeconds(30);
        while (!condition() && RealTime.NowDateTime < deadline)
        {
            await Task.Delay(50, ct);
        }

        Assert.True(condition());
    }

    /// <summary>Runs <paramref name="body"/> on a worker whose host composes the real lock at this test's directory with one slot and <paramref name="fake"/> as the judge block.</summary>
    private async Task Run(FakeBlockStep fake, Func<Rig, Task> body, bool start = true)
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Replacing = services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton(new BlockStepRuntimes([new BlockStepRegistration(_judge)])));
            _ = services.AddKeyedSingleton<IBlockStep>(_judge.Value, fake);
        };
        if (start)
        {
            _harness.Recipe(HeavyRecipes.Id, HeavyRecipes.Heavy());
        }

        await using var sindri = _harness.Sindri(_harness.Replacing, name => name switch { "AW_HEAVY_JOB_LOCK" => _lock, "AW_HEAVY_SLOTS" => "1", _ => null });
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(() => body(new Rig(sindri, server.Environment, _harness)), ct);
    }

    private sealed record Rig(IServiceProvider Sindri, Temporalio.Testing.WorkflowEnvironment Environment, WorkflowsHarness Harness)
    {
        public async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> Start(string item)
        {
            var ct = TestContext.Current.CancellationToken;
            var start = await WorkflowsHarness.RecipeStart(Sindri, HeavyRecipes.Id, item, ct) with
            {
                Inputs = new Dictionary<FactKey, InputFact>
                {
                    [FactKey.From("fact:change")] = new InputFact("change.txt", Harness.Fact("Add a button")),
                    [FactKey.From("fact:diff")] = new InputFact("diff.txt", Harness.Fact("https://example.test/pr/1")),
                },
            };
            return await WorkflowsHarness.Start(Environment.Client, start);
        }
    }
}
