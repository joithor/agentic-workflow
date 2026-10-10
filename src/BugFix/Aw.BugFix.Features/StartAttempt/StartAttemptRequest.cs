using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Features.StartAttempt;

/// <summary>The start-attempt: begin the next fix attempt in a mode.</summary>
/// <param name="StateDirectory">The state directory.</param>
/// <param name="Mode">The attempt's mode.</param>
public sealed record StartAttemptRequest(
    string StateDirectory,
    FixMode Mode);
