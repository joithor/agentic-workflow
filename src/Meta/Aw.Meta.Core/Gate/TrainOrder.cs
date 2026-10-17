using Aw.Meta.Core.Config;

namespace Aw.Meta.Core.Gate;

/// <summary>The order a train takes the merge queue in.</summary>
public static class TrainOrder
{
    /// <summary>The queue in train order: as given, except that a pull request the merge-order rules say must merge after another queued one comes after it.</summary>
    /// <param name="queue">The queued pull request numbers, in queue order.</param>
    /// <param name="rules">The team's merge-order rules.</param>
    /// <returns>The same numbers in train order.</returns>
    public static IReadOnlyList<int> Sort(IReadOnlyList<int> queue, IReadOnlyList<MergeOrderRule> rules)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(rules);
        var remaining = queue.Distinct().ToList();
        var sorted = new List<int>(remaining.Count);
        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(pr => !rules.Any(rule => rule.After == pr && remaining.Contains(rule.Before) && rule.Before != pr), remaining[0]);
            sorted.Add(next);
            _ = remaining.Remove(next);
        }

        return sorted;
    }
}
