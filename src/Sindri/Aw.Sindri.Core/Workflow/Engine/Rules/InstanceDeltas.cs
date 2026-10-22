using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>An event's post-state delta (spec §5.6): what the decision changed, part by part.</summary>
internal static class InstanceDeltas
{
    /// <summary>
    /// The delta from <paramref name="before"/> to <paramref name="after"/>: each changed part's full post-state value.
    /// A part the decision left as it was is the same object, so it compares as unchanged; facts are compared one by one
    /// and only added or replaced, so a fact <paramref name="after"/> lacks is refused.
    /// </summary>
    /// <param name="before">The instance before the decision; null for workflow.started.</param>
    /// <param name="after">The instance after it.</param>
    /// <returns>The delta.</returns>
    /// <exception cref="ArgumentException"><paramref name="after"/> dropped a fact <paramref name="before"/> held.</exception>
    public static InstanceDelta Between(WorkflowInstance? before, WorkflowInstance after)
    {
        ArgumentNullException.ThrowIfNull(after);
        if (before is null)
        {
            return new InstanceDelta(after.Frame, after.Suspension, after.PendingPark, after.Loops, after.Spent, after.Facts, after.Outcome);
        }

        return new InstanceDelta(
            Equals(before.Frame, after.Frame) ? null : after.Frame,
            after.Suspension,
            after.PendingPark,
            SameLoops(before.Loops, after.Loops) ? null : after.Loops,
            SameSpend(before.Spent, after.Spent) ? null : after.Spent,
            FactsDelta(before.Facts, after.Facts),
            after.Outcome == before.Outcome ? null : after.Outcome);
    }

    private static bool SameLoops(IReadOnlyDictionary<string, LoopState> before, IReadOnlyDictionary<string, LoopState> after) =>
        ReferenceEquals(before, after)
        || (before.Count == after.Count && before.All(entry => after.TryGetValue(entry.Key, out var loop) && Equals(loop, entry.Value)));

    private static bool SameSpend(TokenSpend before, TokenSpend after) =>
        ReferenceEquals(before, after)
        || (before.Total == after.Total && before.Granted == after.Granted && before.ByState.Count == after.ByState.Count
            && before.ByState.All(entry => after.ByState.TryGetValue(entry.Key, out var spent) && spent == entry.Value)
            && SameRounds(before.Rounds, after.Rounds) && (before.Overruns ?? []).SequenceEqual(after.Overruns ?? []));

    private static bool SameRounds(IReadOnlyDictionary<string, RoundSpend>? before, IReadOnlyDictionary<string, RoundSpend>? after) =>
        before is null || after is null
            ? before is null && after is null
            : before.Count == after.Count && before.All(entry => after.TryGetValue(entry.Key, out var round) && round == entry.Value);

    private static Dictionary<FactKey, FactValue>? FactsDelta(IReadOnlyDictionary<FactKey, FactValue> before, IReadOnlyDictionary<FactKey, FactValue> after)
    {
        foreach (var key in before.Keys)
        {
            if (!after.ContainsKey(key))
            {
                throw new ArgumentException($"A decision only adds or replaces facts, and this one drops {key.Value} (fix: keep the fact)", nameof(after));
            }
        }

        var changed = after.Where(entry => !before.TryGetValue(entry.Key, out var value) || value != entry.Value).ToDictionary();
        return changed.Count == 0 ? null : changed;
    }
}
