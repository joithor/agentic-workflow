namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>A loop's counters on an instance (spec §3.7, W7): the round it is in, the last round allowed, and the last round's mark.</summary>
/// <param name="Round">The current round, from 1 (entering the looped state first is round 1).</param>
/// <param name="Allowed">The last round allowed: the loop's cap, raised by each '--grant n'.</param>
/// <param name="Last">The last completed round's mark, compared with the next for no-progress.</param>
public sealed record LoopState(int Round, int Allowed, ProgressMark? Last)
{
    /// <summary>Whether '--text' already answered a no-progress park of this loop: it works once (spec §3.8); after that only '--grant n' does.</summary>
    public bool TextAnswered { get; init; }

    /// <summary>How many launches in a row failed in the current round (a launch that never started a session counts no round, spec §3.7); a round counted resets it.</summary>
    public int LaunchFailures { get; init; }
}
