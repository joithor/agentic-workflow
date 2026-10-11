namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// A frame (spec §5.1, W16): the state it is in and everything a pop must restore exactly. <see cref="Entry"/> rises
/// on each real state entry; an engine-owned frame (parked, escalated, taken-over) keeps the entry of the frame it
/// suspends, so its deadlines (takeoverIdle) are keyed under that entry.
/// </summary>
/// <param name="State">A recipe state, or an engine-owned one (<see cref="EngineStates"/>).</param>
/// <param name="Entry">The entry number, from 1.</param>
/// <param name="Run">The open Step run, in an active state.</param>
/// <param name="Child">The child this frame waits on (a child wait) or mirrors (a mirrored park or escalation).</param>
/// <param name="Deadlines">Its armed deadlines.</param>
/// <param name="Since">When the frame was entered (or, for a recipe frame, last resumed).</param>
public sealed record Frame(
    string State,
    int Entry,
    OpenRun? Run,
    WorkflowInstanceId? Child,
    IReadOnlyList<Deadline> Deadlines,
    DateTimeOffset Since);
