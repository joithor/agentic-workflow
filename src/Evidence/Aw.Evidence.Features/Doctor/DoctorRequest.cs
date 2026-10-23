namespace Aw.Evidence.Features.Doctor;

/// <summary>Checks the local QA stack: the app on :3000 is this app, the database is ready, the API port answers; in a worktree with a stack slot, that slot's stack.</summary>
/// <param name="Worktree">The git worktree whose slot to check (UI_EVIDENCE_WORKTREE, else the working directory's checkout), or null for the main stack.</param>
public sealed record DoctorRequest(string? Worktree = null);
