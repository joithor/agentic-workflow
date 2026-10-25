using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A fill reports that it is alive while it works, so a slow but progressing one completes past the old 60 s limit and only a silent one fails (#962).</summary>
[Lane(TestLane.Unit)]
public sealed class StepInputHeartbeatTests
{
    private static readonly BlockId _input = BlockId.From("input.context.lenses");
    private static readonly BlockId _step = BlockId.From("step.scope.gather");

    /// <summary>A fill that finishes only after the test has seen enough beats: it is slow, and it is not stalled.</summary>
    private sealed class SlowRunner(TaskCompletionSource released) : IStepInputRunner
    {
        public async ValueTask<Result<FilledOutput>> Fill(StepInputContext context, int index, CancellationToken cancellationToken)
        {
            await released.Task.WaitAsync(cancellationToken);
            return new FilledOutput(new FilledInput(_input, index, []), new FilledTexts(new Dictionary<string, string>(StringComparer.Ordinal)));
        }
    }

    [Fact]
    public async Task A_slow_fill_heartbeats_until_it_completes()
    {
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var beats = 0;
        var runner = new SlowRunner(released);
        var texts = new MemoryInjectionTexts();
        var activities = new StepInputActivities(runner, texts, new StepContexts(texts, runner, new ItemReposStub()));
        var request = new FillInputRequest("T-1/scoping/1", "gather", "1.1", 1, _input, _step, new Dictionary<string, WithValue>(StringComparer.Ordinal), new Dictionary<FactKey, FactValue>(), "web");

        var running = activities.FillHeartbeating(request, () => { if (Interlocked.Increment(ref beats) == 3) { released.SetResult(); } }, TimeSpan.FromMilliseconds(5), TestContext.Current.CancellationToken);
        var filled = await running.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(_input, filled.Input);
        Assert.True(Volatile.Read(ref beats) >= 3);
    }
}
