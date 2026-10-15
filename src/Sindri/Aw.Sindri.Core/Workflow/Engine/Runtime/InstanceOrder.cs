using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The one order of an instance list (API spec §3.8, E-20), shared by the CLI, the API and the app: each key breaks the
/// ties of the one before. (1) unfinished before finished; (2) not parked or escalated before parked or escalated (a desk
/// task, a taken-over instance and a running one are none of them); (3) the recipe's urgency, incident then triage then
/// work; (4) the subject's label as the row shows it, ordinal (an instance with no subject shows its address); (5) the address, ordinal,
/// so the order is total. Never the start time, never how long it has been in its state.
/// </summary>
public sealed class InstanceOrder : IComparer<InstanceView>
{
    /// <summary>The one instance of the comparer.</summary>
    public static InstanceOrder Instance { get; } = new();

    /// <inheritdoc/>
    public int Compare(InstanceView? x, InstanceView? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            return x is null ? 1 : -1;
        }

        var keys = (Finished(x), Interrupted(x), x.Urgency).CompareTo((Finished(y), Interrupted(y), y.Urgency));
        if (keys != 0)
        {
            return keys;
        }

        var label = string.CompareOrdinal(InstanceSummaries.SubjectOf(x, itemTitle: null).Label, InstanceSummaries.SubjectOf(y, itemTitle: null).Label);
        return label != 0 ? label : string.CompareOrdinal(x.Address.Value, y.Address.Value);
    }

    private static bool Finished(InstanceView view) => view.Outcome is not null;

    private static bool Interrupted(InstanceView view) => view.Question is { Kind: SuspensionKind.Park or SuspensionKind.Escalation };
}
