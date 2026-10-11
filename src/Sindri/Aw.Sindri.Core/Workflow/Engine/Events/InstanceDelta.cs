using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>
/// An event's post-state delta (spec W21): each part of the row the Commit changed, holding that part's full
/// post-state value; a null part didn't change. Folding the deltas over the started instance rebuilds the row.
/// </summary>
/// <param name="Version">The row's version after the Commit.</param>
/// <param name="Frame">The current frame after it.</param>
/// <param name="Suspended">The whole stack after it (empty when it was cleared).</param>
/// <param name="Loops">Every loop's counters after it.</param>
/// <param name="Spent">The token spend after it.</param>
/// <param name="Facts">The facts it added or replaced.</param>
/// <param name="Outcome">The terminal outcome, when it ended the instance.</param>
public sealed record InstanceDelta(
    long Version,
    Frame? Frame,
    IReadOnlyList<SuspendedFrame>? Suspended,
    IReadOnlyDictionary<string, LoopState>? Loops,
    TokenSpend? Spent,
    IReadOnlyDictionary<FactKey, FactValue>? Facts,
    OutcomeName? Outcome);
