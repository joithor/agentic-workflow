namespace Aw.Sindri.Core.Challenges;

/// <summary>A challenge the server issued (engine spec §4.2): one use, valid for <see cref="Lifetime"/>, bound to its action and subject.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Action">The action text the human must type.</param>
/// <param name="Subject">What it is bound to.</param>
/// <param name="ExpiresAt">When it stops being valid.</param>
public sealed record IssuedChallenge(ChallengeId Id, ChallengeAction Action, ChallengeSubject Subject, DateTimeOffset ExpiresAt)
{
    /// <summary>How long a challenge is valid: 5 minutes.</summary>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(5);
}
