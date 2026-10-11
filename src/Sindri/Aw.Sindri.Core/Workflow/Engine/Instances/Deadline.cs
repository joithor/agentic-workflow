namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// An armed deadline (spec §5.1, W18, W19): due at <see cref="Due"/> while it runs; paused by a takeover, which keeps
/// <see cref="Remaining"/> and clears <see cref="Due"/>. A park or escalation clears the frame's deadlines.
/// </summary>
/// <param name="Id">'{entry}:{kind}:{arm}', also its trigger id.</param>
/// <param name="Due">When it fires; null while paused.</param>
/// <param name="Remaining">What was left when a takeover paused it; null while it runs.</param>
public sealed record Deadline(DeadlineId Id, DateTimeOffset? Due, TimeSpan? Remaining)
{
    /// <summary>Whether a takeover paused it.</summary>
    public bool Paused => Due is null;
}
