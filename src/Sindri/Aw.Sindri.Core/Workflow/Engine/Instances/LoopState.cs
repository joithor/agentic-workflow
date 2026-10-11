namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>A loop's counters on an instance (spec §3.7, W7): the round it is in, the last round allowed, and the last round's mark.</summary>
/// <param name="Round">The current round, from 1 (entering the looped state first is round 1).</param>
/// <param name="Allowed">The last round allowed: the loop's cap, raised by each '--grant n'.</param>
/// <param name="Last">The last completed round's mark, compared with the next for no-progress.</param>
public sealed record LoopState(int Round, int Allowed, ProgressMark? Last);
