namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The one order of an instance list (API spec §3.8, E-20), shared by the CLI, the API and the app: each key breaks the
/// ties of the one before. (1) unfinished before finished; (2) not parked or escalated before parked or escalated (a desk
/// task, a taken-over instance and a running one are none of them); (3) the recipe's urgency, incident then triage then
/// work; (4) the subject's label, ordinal (an instance with no subject sorts as an empty label); (5) the address, ordinal,
/// so the order is total. Never the start time, never how long it has been in its state.
/// </summary>
public sealed class InstanceOrder : IComparer<InstanceView>
{
    /// <summary>The one instance of the comparer.</summary>
    public static InstanceOrder Instance { get; } = new();

    /// <inheritdoc/>
    public int Compare(InstanceView? x, InstanceView? y) => throw new NotSupportedException("red: the list order isn't built yet");
}
