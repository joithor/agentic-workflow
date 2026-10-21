using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A worker restart (line spec LN-WF3): the lease is lost with the process, the instance is still in its heavy phase, so its next Step off the desk takes a slot before it runs.</summary>
[Lane(TestLane.Integration)]
public sealed class HeavyRestartTests(BlockRuntimeDevServer server) : IClassFixture<BlockRuntimeDevServer>, IDisposable
{
    private readonly HeavyRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task After_a_worker_restart_the_next_Step_takes_a_slot_before_it_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runs = 0;
        var fake = new FakeBlockStep(async (_, token) =>
        {
            if (Interlocked.Increment(ref runs) == 1)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);   // dies with the first worker
            }

            await gate.Task.WaitAsync(token);
            return HeavyRig.FakeResult();
        });
        await using var sindri = await _rig.Compose(fake);
        WorkflowHandle<RecipeWorkflow, RecipeResult>? handle = null;
        using (var first = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>()))
        {
            await first.ExecuteAsync(async () =>
            {
                handle = await _rig.Start(sindri, server.Environment);
                await HeavyRig.Eventually(() => fake.Runs == 1 && Directory.Exists(_rig.Lock), ct);
            }, ct);   // the worker stops with the instance in its heavy phase
        }

        await sindri.GetRequiredService<HeavyLeases>().DisposeAsync();   // the process that held the lease is gone
        Assert.False(Directory.Exists(_rig.Lock));

        using var second = new TemporalWorker(server.Environment.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await second.ExecuteAsync(async () =>
        {
            _ = await WorkflowsHarness.Until(handle!, static v => v.State == "second");   // the run that died with the first worker failed; the instance moves on
            await HeavyRig.Eventually(() => fake.Runs == 2 && Directory.Exists(_rig.Lock), ct);   // a slot was taken again before 'second' ran

            Assert.True(fake.Last!.HeavySlotHeld);
            gate.SetResult();
            _ = await WorkflowsHarness.Until(handle!, static v => v.State == "desk");
            await handle!.TerminateAsync("test over");
        }, ct);
    }
}
