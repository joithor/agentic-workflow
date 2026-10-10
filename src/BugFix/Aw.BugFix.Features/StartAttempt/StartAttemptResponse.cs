using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Features.StartAttempt;

/// <summary>The result of start-attempt.</summary>
/// <param name="Phase">The new phase.</param>
/// <param name="Attempt">The attempt number.</param>
/// <param name="Mode">The attempt's mode.</param>
public sealed record StartAttemptResponse(
    Phase Phase,
    int Attempt,
    FixMode Mode);
