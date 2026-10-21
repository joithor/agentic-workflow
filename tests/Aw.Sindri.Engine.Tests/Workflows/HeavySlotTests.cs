using Aw.Kernel.Testing;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The heavy resource on the time-skipping server (line spec LN-WF3, §8.4) over the real directory lock with one slot: a 'waitFor: { resource: heavy }'
/// takes the slot, the instance holds it while it runs Steps off the desk, and gives it up at a desk Step, when it ends or is cancelled; a Step after
/// a desk Step takes one first; a command cuts a wait for a slot short and a handback waits again.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class HeavySlotTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly HeavyRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task A_second_instance_waits_for_the_slot_until_the_first_gives_it_up_and_a_Step_knows_it_holds_one()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = FakeBlockStep.Gated(gate.Task, "ui");
        await Run(fake, async sindri =>
        {
            var first = await _rig.Start(sindri, server.Environment);
            _ = await WorkflowsHarness.Until(first, static v => v.State == "first");
            await HeavyRig.Eventually(() => fake.Runs == 1 && Directory.Exists(_rig.Lock), ct);
            var second = await _rig.Start(sindri, server.Environment);
            _ = await WorkflowsHarness.Until(second, static v => v.State == "slot");
            await Task.Delay(TimeSpan.FromSeconds(2), ct);   // the lock's poll is one second: a second instance that could have taken the slot would have

            Assert.Equal((1, "slot"), (fake.Runs, (await WorkflowsHarness.Until(second, static _ => true)).State));
            Assert.True(fake.Last!.HeavySlotHeld);

            gate.SetResult();   // the first runs its Steps to the desk and gives the slot up; the second takes it
            _ = await WorkflowsHarness.Until(first, static v => v.State == "desk");
            _ = await WorkflowsHarness.Until(second, static v => v.State == "desk");
            Assert.Equal(4, fake.Runs);
            await HeavyRig.Eventually(() => !Directory.Exists(_rig.Lock), ct);
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

            return HeavyRig.FakeResult();
        });
        await Run(fake, async sindri =>
        {
            var handle = await _rig.Start(sindri, server.Environment);
            var desk = await WorkflowsHarness.Until(handle, static v => v.State == "desk" && v.Run is not null);
            await HeavyRig.Eventually(() => !Directory.Exists(_rig.Lock), ct);   // the desk Step does not hold the box's slot

            _ = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(HeavyRig.DeskReport(desk.Run!.Value)), new WorkflowUpdateOptions(new string('d', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "third");
            await HeavyRig.Eventually(() => fake.Runs == 3 && Directory.Exists(_rig.Lock), ct);   // taken again before the Step ran
            Assert.True(fake.Last!.HeavySlotHeld);

            gate.SetResult();
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "done");
            await HeavyRig.Eventually(() => !Directory.Exists(_rig.Lock), ct);
            await HeavyRig.Eventually(() => HeavyRig.Rows(sindri, ct).GetAwaiter().GetResult().Count == 4, ct);
            Assert.Equal(["heavy.held", "heavy.released", "heavy.held", "heavy.released"], await HeavyRig.Rows(sindri, ct));
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task Cancelling_an_instance_that_holds_the_slot_releases_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = FakeBlockStep.Hanging();
        await Run(fake, async sindri =>
        {
            var handle = await _rig.Start(sindri, server.Environment);
            await HeavyRig.Eventually(() => fake.Runs == 1 && Directory.Exists(_rig.Lock), ct);

            _ = await ChildTree.Cancel(handle);

            await HeavyRig.Eventually(() => !Directory.Exists(_rig.Lock), ct);
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_takeover_cuts_a_wait_for_a_slot_short_and_the_handback_waits_for_it_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var fake = FakeBlockStep.Completing("ui");
        await Run(fake, async sindri =>
        {
            var blocker = await sindri.GetRequiredService<IHeavySlots>().Take("test blocker", TimeSpan.FromSeconds(5), waiting: null, ct);
            var handle = await _rig.Start(sindri, server.Environment);
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "slot");
            var takeover = new TakeoverRequested("human:cli", new CommandStamp(CommandNonce.From(new string('1', 32)), new string('0', 64)));
            _ = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(takeover), new WorkflowUpdateOptions(new string('1', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "taken-over");
            Assert.Equal(0, fake.Runs);

            await blocker.Value.Lease!.DisposeAsync();
            var handback = new HandbackRequested("human:cli", new CommandStamp(CommandNonce.From(new string('2', 32)), new string('0', 64)));
            _ = await handle.ExecuteUpdateAsync(wf => wf.HandbackAsync(handback), new WorkflowUpdateOptions(new string('2', 32)));
            _ = await WorkflowsHarness.Until(handle, static v => v.State == "desk");
            Assert.Equal(2, fake.Runs);
        });
    }

    private async Task Run(FakeBlockStep fake, Func<IServiceProvider, Task> body)
    {
        await using var sindri = await _rig.Compose(fake);
        using var worker = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(() => body(sindri), TestContext.Current.CancellationToken);
    }
}
