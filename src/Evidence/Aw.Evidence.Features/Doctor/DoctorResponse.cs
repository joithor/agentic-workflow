namespace Aw.Evidence.Features.Doctor;

/// <summary>The stack's health.</summary>
/// <param name="Healthy">Whether every check passed.</param>
/// <param name="Failures">One line per failed check.</param>
/// <param name="Slot">The worktree slot that was checked: its host and database; null for the main stack.</param>
public sealed record DoctorResponse(bool Healthy, IReadOnlyList<string> Failures, DoctorSlot? Slot = null);
