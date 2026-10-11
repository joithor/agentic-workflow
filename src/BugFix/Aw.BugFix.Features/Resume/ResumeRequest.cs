namespace Aw.BugFix.Features.Resume;

/// <summary>The resume: where the bug fix stands and what to do next.</summary>
/// <param name="StateDirectory">The state directory.</param>
public sealed record ResumeRequest(
    string StateDirectory);
