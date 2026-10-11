using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Features.Resume;

/// <summary>The result of resume.</summary>
/// <param name="Phase">The phase.</param>
/// <param name="Status">The status.</param>
/// <param name="Attempt">The attempt.</param>
/// <param name="Next">The next action.</param>
public sealed record ResumeResponse(
    Phase Phase,
    BugFixStatus Status,
    int Attempt,
    string Next);
