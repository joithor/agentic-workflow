using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;

namespace Aw.Meta.Tests.Gate;

public sealed class StepSchedulerTests
{
    private static readonly TimeSpan _patience = TimeSpan.FromSeconds(10);

    /// <summary>A fake step runner: every step announces it started, then waits until the test lets it finish.</summary>
    private sealed class Gates
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource> _started = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<Result<string>>> _release = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<string> _order = new();
        private int _running;
        private int _peak;

        public int Peak => _peak;

        public IReadOnlyList<string> StartOrder => [.. _order];

        public async ValueTask<Result<string>> Run(StepConfig step, CancellationToken cancellationToken)
        {
            _order.Enqueue(step.Name);
            var now = Interlocked.Increment(ref _running);
            int seen;
            while ((seen = Volatile.Read(ref _peak)) < now && Interlocked.CompareExchange(ref _peak, now, seen) != seen)
            {
            }

            _ = Started(step.Name).TrySetResult();
            try
            {
                return await Release(step.Name).Task.WaitAsync(cancellationToken);
            }
            finally
            {
                _ = Interlocked.Decrement(ref _running);
            }
        }

        public async Task WaitStarted(string name) => await Started(name).Task.WaitAsync(_patience, TimeProvider.System, TestContext.Current.CancellationToken);

        public void Finish(string name, Result<string>? result = null) => Release(name).TrySetResult(result ?? name);

        public async Task NotStarted(string name)
        {
            await Task.Yield();
            await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, TestContext.Current.CancellationToken);
            Assert.False(Started(name).Task.IsCompleted, name + " started early");
        }

        private TaskCompletionSource Started(string name) => _started.GetOrAdd(name, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

        private TaskCompletionSource<Result<string>> Release(string name) => _release.GetOrAdd(name, _ => new TaskCompletionSource<Result<string>>(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    private static StepConfig Heavy(string name) => new(name, ["x", name], Heavy: true, After: []);

    private static StepGraph Graph(params StepConfig[] steps) => StepGraph.Build(steps, [.. steps.Select(s => s.Name)]).Value;

    private static Task<Result<IReadOnlyList<string>>> Start(Gates gates, StepGraph graph, int limit) =>
        StepScheduler.Run(graph, limit, gates.Run, TestContext.Current.CancellationToken);

    [Fact, Trait("Lane", "unit")]
    public async Task Independent_steps_run_at_the_same_time_up_to_the_limit()
    {
        var gates = new Gates();
        var run = Start(gates, Graph(Heavy("a"), Heavy("b"), Heavy("c")), limit: 2);

        await gates.WaitStarted("a");
        await gates.WaitStarted("b");
        await gates.NotStarted("c");
        gates.Finish("a");
        await gates.WaitStarted("c");
        gates.Finish("b");
        gates.Finish("c");
        _ = await run.WaitAsync(_patience, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.Equal(2, gates.Peak);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_limit_of_one_runs_the_steps_one_after_the_other_in_config_order()
    {
        var gates = new Gates();
        var run = Start(gates, Graph(Heavy("a"), Heavy("b")), limit: 1);

        await gates.WaitStarted("a");
        await gates.NotStarted("b");
        gates.Finish("a");
        await gates.WaitStarted("b");
        gates.Finish("b");
        _ = await run.WaitAsync(_patience, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.Equal(1, gates.Peak);
        Assert.Equal(["a", "b"], gates.StartOrder);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cheap_steps_do_not_use_a_heavy_slot()
    {
        var gates = new Gates();
        var cheap = new StepConfig("cheap", ["x"], After: []);
        var run = Start(gates, Graph(Heavy("a"), cheap), limit: 1);

        await gates.WaitStarted("a");
        await gates.WaitStarted("cheap");
        gates.Finish("a");
        gates.Finish("cheap");
        _ = await run.WaitAsync(_patience, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.Equal(2, gates.Peak);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_starts_only_after_everything_it_waits_for_finished()
    {
        var gates = new Gates();
        var build = Heavy("build");
        var unit = Heavy("unit") with { After = ["build"] };
        var sync = new StepConfig("sync", ["x"], After: []);
        var run = Start(gates, Graph(sync, build, unit), limit: 3);

        await gates.WaitStarted("sync");
        await gates.WaitStarted("build");
        await gates.NotStarted("unit");
        gates.Finish("sync");
        await gates.NotStarted("unit");
        gates.Finish("build");
        await gates.WaitStarted("unit");
        gates.Finish("unit");
        _ = await run.WaitAsync(_patience, TimeProvider.System, TestContext.Current.CancellationToken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Results_come_back_in_config_order_whatever_order_the_steps_finish_in()
    {
        var gates = new Gates();
        var run = Start(gates, Graph(Heavy("a"), Heavy("b"), Heavy("c")), limit: 3);

        await gates.WaitStarted("c");
        gates.Finish("c");
        gates.Finish("b");
        gates.Finish("a");
        var result = await run.WaitAsync(_patience, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b", "c"], result.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_error_cancels_the_steps_still_running_and_is_what_comes_back()
    {
        var gates = new Gates();
        var run = Start(gates, Graph(Heavy("a"), Heavy("b")), limit: 2);

        await gates.WaitStarted("a");
        await gates.WaitStarted("b");
        gates.Finish("b", MetaErrors.CommandNotStarted.With("no such program"));
        var result = await run.WaitAsync(_patience, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("no such program", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cancelling_the_run_cancels_every_step()
    {
        var gates = new Gates();
        using var cts = new CancellationTokenSource();
        var run = StepScheduler.Run(Graph(Heavy("a")), 1, gates.Run, cts.Token);

        await gates.WaitStarted("a");
        await cts.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(_patience, TimeProvider.System, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_graph_yields_no_results()
    {
        var result = await StepScheduler.Run(Graph(), 1, (_, _) => ValueTask.FromResult<Result<string>>("x"), CancellationToken.None);

        Assert.Empty(result.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_that_throws_is_an_error_that_cancels_the_steps_still_running_and_holds_back_its_dependents()
    {
        var gates = new Gates();
        var boom = Heavy("boom");
        var after = Heavy("after") with { After = ["boom"] };
        var run = StepScheduler.Run(
            Graph(boom, Heavy("sibling"), after),
            3,
            (step, token) => step.Name == "boom" ? throw new IOException("disk full") : gates.Run(step, token),
            TestContext.Current.CancellationToken);

        var result = await run.WaitAsync(_patience, TimeProvider.System, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("disk full", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("after", gates.StartOrder, StringComparer.Ordinal);
    }
}
