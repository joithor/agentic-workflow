namespace Aw.Context.Core.Walks;

/// <summary>
/// Runs independent, read-only work items with a bounded number in flight and hands the results back in input order, so a caller that merges them in that order gets the answer a serial run gives.
/// </summary>
public static class BoundedParallel
{
    /// <summary>Runs <paramref name="work"/> over every item, at most <paramref name="degree"/> at once.</summary>
    /// <typeparam name="TIn">The item type.</typeparam>
    /// <typeparam name="TOut">The result type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="work">The work for one item; it must only read shared state.</param>
    /// <param name="degree">The most items in flight; below 2 (or fewer than two items) runs them one at a time.</param>
    /// <param name="cancellationToken">Stops the run; items not yet started are not started.</param>
    /// <returns>One result per item, at the item's index.</returns>
    public static ValueTask<TOut[]> Map<TIn, TOut>(IReadOnlyList<TIn> items, Func<TIn, CancellationToken, ValueTask<TOut>> work, int degree, CancellationToken cancellationToken) =>
        throw new NotSupportedException("not built yet");

    /// <summary>Runs <paramref name="work"/> over every item on the calling thread and the pool, at most <paramref name="degree"/> at once.</summary>
    /// <typeparam name="TIn">The item type.</typeparam>
    /// <typeparam name="TOut">The result type.</typeparam>
    /// <param name="items">The items.</param>
    /// <param name="work">The work for one item; it must only read shared state.</param>
    /// <param name="degree">The most items in flight; below 2 (or fewer than two items) runs them one at a time.</param>
    /// <returns>One result per item, at the item's index.</returns>
    public static TOut[] MapSync<TIn, TOut>(IReadOnlyList<TIn> items, Func<TIn, TOut> work, int degree) =>
        throw new NotSupportedException("not built yet");
}
