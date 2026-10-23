namespace Aw.Evidence.Features.Doctor;

/// <summary>The worktree slot doctor checked, and what to pass to the runner.</summary>
/// <param name="Number">The slot, 1 or more.</param>
/// <param name="Worktree">The worktree that holds the slot file.</param>
/// <param name="Host">The --host to pass to the runner (http://localhost:&lt;3000 + step * slot&gt;).</param>
/// <param name="Database">The slot's database to check provenance against, or null when none is configured.</param>
public sealed record DoctorSlot(int Number, string Worktree, string Host, string? Database);
