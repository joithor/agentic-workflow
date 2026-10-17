using Aw.Kernel;
using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Gate;

/// <summary>Runs a <see cref="StepGraph"/>: a step starts when everything it waits for has finished.</summary>
public static class StepScheduler
{
    /// <summary>Runs every step; heavy steps are limited to <paramref name="limit"/> at once, other steps start as soon as they are free.</summary>
    /// <typeparam name="T">What a step yields.</typeparam>
    /// <param name="graph">The steps and their order of dependence.</param>
    /// <param name="limit">The most heavy steps running at once (at least 1).</param>
    /// <param name="run">Runs one step.</param>
    /// <param name="cancellationToken">Cancels every running step.</param>
    /// <returns>The results in config order, or the first error (by config order); an error cancels the steps still running.</returns>
    public static Task<Result<IReadOnlyList<T>>> Run<T>(StepGraph graph, int limit, Func<StepConfig, CancellationToken, ValueTask<Result<T>>> run, CancellationToken cancellationToken) =>
        Run(graph, new SlotPool(limit), 0, run, cancellationToken);

    /// <summary>Runs every step; heavy steps take a slot of the shared <paramref name="slots"/>, so several runs can share one limit.</summary>
    /// <typeparam name="T">What a step yields.</typeparam>
    /// <param name="graph">The steps and their order of dependence.</param>
    /// <param name="slots">The heavy slots, shared with other runs.</param>
    /// <param name="job">This run's rank in the pool: a free slot goes to the lowest job first, then the lowest step index.</param>
    /// <param name="run">Runs one step.</param>
    /// <param name="cancellationToken">Cancels every running step.</param>
    /// <returns>The results in config order, or the first error (by config order); an error cancels the steps still running.</returns>
    public static async Task<Result<IReadOnlyList<T>>> Run<T>(StepGraph graph, SlotPool slots, int job, Func<StepConfig, CancellationToken, ValueTask<Result<T>>> run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(run);
        var count = graph.Steps.Count;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var done = Enumerable.Range(0, count).Select(_ => new TaskCompletionSource<Result<T>?>(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();

        await Task.WhenAll(Enumerable.Range(0, count).Select(i => Node(new Shared<T>(graph, done, slots, job, run, stop), i))).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<T>(count);
        foreach (var task in done)
        {
            var outcome = await task.Task.ConfigureAwait(false);
            if (outcome is null)
            {
                continue;
            }

            if (outcome.IsError)
            {
                return outcome.Error;
            }

            results.Add(outcome.Value);
        }

        return results;
    }

    private static async Task Node<T>(Shared<T> all, int i)
    {
        var (graph, done, slots, job, run, stop) = all;
        Result<T>? outcome = null;
        try
        {
            foreach (var dependency in graph.DependenciesOf(i))
            {
                _ = await done[dependency].Task.WaitAsync(stop.Token).ConfigureAwait(false);
            }

            outcome = await RunOne(graph.Steps[i], (slots, job, i), run, stop.Token).ConfigureAwait(false);
            if (outcome.IsError)
            {
                await stop.CancelAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // Cancelled by a sibling's error or by the caller; the error (or the caller's exception) is reported by Run.
        }
        finally
        {
            done[i].SetResult(outcome);
        }
    }

    private static async Task<Result<T>> RunOne<T>(StepConfig step, (SlotPool Pool, int Job, int Index) slots, Func<StepConfig, CancellationToken, ValueTask<Result<T>>> run, CancellationToken cancellationToken)
    {
        if (!step.Heavy)
        {
            return await run(step, cancellationToken).ConfigureAwait(false);
        }

        using var slot = await slots.Pool.Acquire(slots.Job, slots.Index, cancellationToken).ConfigureAwait(false);
        return await run(step, cancellationToken).ConfigureAwait(false);
    }

    private sealed record Shared<T>(StepGraph Graph, TaskCompletionSource<Result<T>?>[] Done, SlotPool Slots, int Job, Func<StepConfig, CancellationToken, ValueTask<Result<T>>> Run, CancellationTokenSource Stop);
}
