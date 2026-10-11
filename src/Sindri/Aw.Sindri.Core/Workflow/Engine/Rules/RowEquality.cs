using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Structural equality of instance rows and their parts (spec W21): what the delta compares and the fold tests assert.
/// Records compare their lists and dictionaries by reference, so every collection in a row is compared here by value.
/// A suspended frame's resume action and pending park are compared as records: a withheld trigger's payload compares
/// by reference, which at worst puts an unchanged stack in a delta (harmless to the fold).
/// </summary>
internal static class RowEquality
{
    /// <summary>Whether <paramref name="left"/> and <paramref name="right"/> hold the same row, part by part.</summary>
    /// <param name="left">One row.</param>
    /// <param name="right">The other.</param>
    /// <returns>True when every part is equal.</returns>
    public static bool Same(WorkflowInstance left, WorkflowInstance right) =>
        left with { Recipe = right.Recipe, Frame = right.Frame, Suspended = right.Suspended, Loops = right.Loops, Spent = right.Spent, Facts = right.Facts } == right
        && SameRecipe(left.Recipe, right.Recipe) && SameFrame(left.Frame, right.Frame) && SameStack(left.Suspended, right.Suspended)
        && SameLoops(left.Loops, right.Loops) && SameSpend(left.Spent, right.Spent) && SameMap(left.Facts, right.Facts);

    /// <summary>Whether two frames are equal, their deadlines by value.</summary>
    /// <param name="left">One frame.</param>
    /// <param name="right">The other.</param>
    /// <returns>True when equal.</returns>
    public static bool SameFrame(Frame left, Frame right) =>
        ReferenceEquals(left, right) || (left with { Deadlines = right.Deadlines } == right && left.Deadlines.SequenceEqual(right.Deadlines));

    /// <summary>Whether two stacks hold the same frames, bottom first.</summary>
    /// <param name="left">One stack.</param>
    /// <param name="right">The other.</param>
    /// <returns>True when equal.</returns>
    public static bool SameStack(IReadOnlyList<SuspendedFrame> left, IReadOnlyList<SuspendedFrame> right) =>
        ReferenceEquals(left, right) || (left.Count == right.Count && left.Zip(right).All(pair =>
            SameFrame(pair.First.Frame, pair.Second.Frame) && pair.First with { Frame = pair.Second.Frame } == pair.Second));

    /// <summary>Whether two loop maps hold the same counters.</summary>
    /// <param name="left">One map.</param>
    /// <param name="right">The other.</param>
    /// <returns>True when equal.</returns>
    public static bool SameLoops(IReadOnlyDictionary<string, LoopState> left, IReadOnlyDictionary<string, LoopState> right) => SameMap(left, right);

    /// <summary>Whether two spends are equal, per state by value.</summary>
    /// <param name="left">One spend.</param>
    /// <param name="right">The other.</param>
    /// <returns>True when equal.</returns>
    public static bool SameSpend(TokenSpend left, TokenSpend right) =>
        ReferenceEquals(left, right) || (left.Total == right.Total && left.Granted == right.Granted && SameMap(left.ByState, right.ByState));

    /// <summary>Whether two maps hold the same keys with equal values.</summary>
    /// <typeparam name="TKey">The key.</typeparam>
    /// <typeparam name="TValue">The value.</typeparam>
    /// <param name="left">One map.</param>
    /// <param name="right">The other.</param>
    /// <returns>True when equal.</returns>
    public static bool SameMap<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> left, IReadOnlyDictionary<TKey, TValue> right)
        where TKey : notnull =>
        ReferenceEquals(left, right) || (left.Count == right.Count && left.All(entry =>
            right.TryGetValue(entry.Key, out var other) && EqualityComparer<TValue>.Default.Equals(entry.Value, other)));

    private static bool SameRecipe(PinnedRecipe left, PinnedRecipe right) =>
        left with { Children = right.Children } == right && SameMap(left.Children, right.Children);
}
