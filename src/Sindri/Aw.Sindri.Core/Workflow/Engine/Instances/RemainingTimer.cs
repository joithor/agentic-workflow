namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// The state timer a takeover paused (engine spec §5.5): which timer it was and what was left of it when the takeover
/// cancelled its scope, measured from 'Workflow.UtcNow'. It rides on the takeover's <see cref="Suspension"/>, so the
/// handback re-arms that timer with <see cref="Left"/> (not the full timeout), and status shows it ('paused timeout 40s
/// left', §6.4). A takeover over a park or escalation pauses nothing: a park already stopped the timer (§5.5), and the
/// answer that ends the park arms the full timeout again. Engine P5 contract unit.
/// </summary>
/// <param name="Kind">The paused timer: <see cref="DeadlineKind.State"/>, or <see cref="DeadlineKind.Retry"/> after a 'then: retry' re-armed it.</param>
/// <param name="Left">What was left of it, never negative.</param>
public sealed record RemainingTimer(DeadlineKind Kind, TimeSpan Left);
