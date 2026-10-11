using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// An instance of a recipe (spec §5.1): the row every Commit changes and every event's post-state delta rebuilds
/// (W21). <see cref="Frame"/> is the current frame; <see cref="Suspended"/> is the stack under it (bottom first), at
/// most two deep: a park or escalation on the base frame, and a takeover on top of either.
/// </summary>
/// <param name="Id">The opaque id.</param>
/// <param name="Address">The CLI address.</param>
/// <param name="Tenant">The tenant key.</param>
/// <param name="Ring">The ring.</param>
/// <param name="Recipe">The recipe and its pinned children.</param>
/// <param name="Item">The item.</param>
/// <param name="Attempt">The attempt, from 1.</param>
/// <param name="Parent">The parent, for a child.</param>
/// <param name="Frame">The current frame.</param>
/// <param name="Suspended">The suspended frames, bottom first.</param>
/// <param name="Loops">Each loop's counters, by loop name.</param>
/// <param name="Spent">The token spend.</param>
/// <param name="Facts">The facts, as stored references.</param>
/// <param name="Version">Rises by one on every Commit.</param>
/// <param name="Outcome">The terminal outcome, once the instance ended (a recipe terminal's, or 'cancelled').</param>
public sealed record WorkflowInstance(
    WorkflowInstanceId Id,
    InstanceAddress Address,
    string Tenant,
    int Ring,
    PinnedRecipe Recipe,
    ItemKey Item,
    int Attempt,
    WorkflowInstanceId? Parent,
    Frame Frame,
    IReadOnlyList<SuspendedFrame> Suspended,
    IReadOnlyDictionary<string, LoopState> Loops,
    TokenSpend Spent,
    IReadOnlyDictionary<FactKey, FactValue> Facts,
    long Version,
    OutcomeName? Outcome)
{
    /// <summary>The state status shows: the current frame's (an engine-owned state while suspended).</summary>
    [JsonIgnore]
    public string State => Frame.State;

    /// <summary>The park reason status shows, while the current frame is a park.</summary>
    [JsonIgnore]
    public ParkReason? Park => Frame.State == EngineStates.Parked && Suspended.Count > 0 ? Suspended[^1].Suspension.Reason : null;

    /// <summary>Whether the instance has ended.</summary>
    [JsonIgnore]
    public bool Ended => Outcome is not null;
}
