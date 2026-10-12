using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>
/// An event's post-state delta (spec §5.6): each part of the instance the decision changed, holding that part's full
/// post-state value; a null part didn't change. <see cref="Suspension"/> and <see cref="PendingPark"/> are always their
/// post-state values, null when there is none, so a delta shows a suspension ending.
/// </summary>
/// <param name="Frame">The recipe frame after it.</param>
/// <param name="Suspension">The suspension after it.</param>
/// <param name="PendingPark">The pending park after it.</param>
/// <param name="Loops">Every loop's counters after it.</param>
/// <param name="Spent">The token spend after it.</param>
/// <param name="Facts">The facts it added or replaced.</param>
/// <param name="Outcome">The terminal outcome, when it ended the instance.</param>
public sealed record InstanceDelta(
    Frame? Frame,
    Suspension? Suspension,
    Suspension? PendingPark,
    IReadOnlyDictionary<string, LoopState>? Loops,
    TokenSpend? Spent,
    IReadOnlyDictionary<FactKey, FactValue>? Facts,
    OutcomeName? Outcome);
