using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Replay (spec W21, §10 'Replay reproduces the instance'): every event carries the full post-state delta, so folding
/// an instance's events from its workflow.started rebuilds the row exactly. P3 unit (e).
/// </summary>
internal static class EventFold
{
    /// <summary>
    /// The delta from <paramref name="before"/> to <paramref name="after"/>: each changed part's full post-state value
    /// (parts compared by value), and the version <paramref name="after"/> carries, which the caller has already raised.
    /// Facts are only added or replaced: a fact <paramref name="after"/> lacks can't be folded, and is refused.
    /// </summary>
    /// <param name="before">The row before the Commit; null for workflow.started.</param>
    /// <param name="after">The row after it.</param>
    /// <returns>The delta.</returns>
    /// <exception cref="ArgumentException"><paramref name="after"/> dropped a fact <paramref name="before"/> held.</exception>
    public static InstanceDelta Delta(WorkflowInstance? before, WorkflowInstance after)
    {
        ArgumentNullException.ThrowIfNull(after);
        if (before is null)
        {
            return new InstanceDelta(after.Version, after.Frame, after.Suspended, after.Loops, after.Spent, after.Facts, after.Outcome);
        }

        return new InstanceDelta(
            after.Version,
            RowEquality.SameFrame(before.Frame, after.Frame) ? null : after.Frame,
            RowEquality.SameStack(before.Suspended, after.Suspended) ? null : after.Suspended,
            RowEquality.SameLoops(before.Loops, after.Loops) ? null : after.Loops,
            RowEquality.SameSpend(before.Spent, after.Spent) ? null : after.Spent,
            FactsDelta(before.Facts, after.Facts),
            after.Outcome == before.Outcome ? null : after.Outcome);
    }

    /// <summary>Applies one event: workflow.started yields its row; every other event applies its delta to <paramref name="before"/>.</summary>
    /// <param name="before">The row so far; null before workflow.started.</param>
    /// <param name="recorded">The event.</param>
    /// <returns>The row after it.</returns>
    /// <exception cref="ArgumentException">The event doesn't follow <paramref name="before"/>: another instance, another version, or a start out of place.</exception>
    public static WorkflowInstance Apply(WorkflowInstance? before, WorkflowEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        if (recorded.Type == WorkflowEventType.Started)
        {
            return before is null && recorded.Started is { } started
                ? started
                : throw Broken(recorded, before is null ? "workflow.started carries no row" : "a second workflow.started");
        }

        return before is null
            ? throw Broken(recorded, "the log doesn't start with workflow.started")
            : Patch(Follow(before, recorded), recorded.Delta);
    }

    /// <summary>Folds an instance's events, oldest first, from its workflow.started.</summary>
    /// <param name="events">The events.</param>
    /// <returns>The row they rebuild.</returns>
    /// <exception cref="ArgumentException">There are no events, or one doesn't follow the row before it.</exception>
    public static WorkflowInstance Fold(IEnumerable<WorkflowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        WorkflowInstance? row = null;
        foreach (var recorded in events)
        {
            row = Apply(row, recorded);
        }

        return row ?? throw new ArgumentException("An instance's log starts with workflow.started; this one is empty", nameof(events));
    }

    private static WorkflowInstance Follow(WorkflowInstance before, WorkflowEvent recorded) =>
        recorded.Instance == before.Id && recorded.FromVersion == before.Version && recorded.Delta.Version == recorded.ToVersion
            ? before
            : throw Broken(recorded, string.Create(CultureInfo.InvariantCulture, $"it goes from version {recorded.FromVersion} of {recorded.Instance.Value}, the row is at {before.Version} of {before.Id.Value}"));

    private static WorkflowInstance Patch(WorkflowInstance before, InstanceDelta delta) => before with
    {
        Version = delta.Version,
        Frame = delta.Frame ?? before.Frame,
        Suspended = delta.Suspended ?? before.Suspended,
        Loops = delta.Loops ?? before.Loops,
        Spent = delta.Spent ?? before.Spent,
        Facts = delta.Facts is { } facts ? Merge(before.Facts, facts) : before.Facts,
        Outcome = delta.Outcome ?? before.Outcome,
    };

    private static Dictionary<FactKey, FactValue>? FactsDelta(IReadOnlyDictionary<FactKey, FactValue> before, IReadOnlyDictionary<FactKey, FactValue> after)
    {
        foreach (var key in before.Keys)
        {
            if (!after.ContainsKey(key))
            {
                throw new ArgumentException($"A Commit only adds or replaces facts, and this one drops {key.Value} (fix: keep the fact)", nameof(after));
            }
        }

        var changed = after.Where(entry => !before.TryGetValue(entry.Key, out var value) || value != entry.Value).ToDictionary();
        return changed.Count == 0 ? null : changed;
    }

    private static Dictionary<FactKey, FactValue> Merge(IReadOnlyDictionary<FactKey, FactValue> before, IReadOnlyDictionary<FactKey, FactValue> changed)
    {
        var merged = before.ToDictionary();
        foreach (var (key, value) in changed)
        {
            merged[key] = value;
        }

        return merged;
    }

    private static ArgumentException Broken(WorkflowEvent recorded, string why) =>
        new($"Event {recorded.Trigger.Key.Value} ({recorded.Type}) doesn't follow the row: {why}", nameof(recorded));
}
