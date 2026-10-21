namespace Aw.Sindri.Core.Challenges;

/// <summary>A challenge the server issued (engine spec §4.2): one use, valid for <see cref="Lifetime"/>, bound to its action and subject, confirmed in its mode on its channel.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Action">The action text the human must type.</param>
/// <param name="Subject">What it is bound to.</param>
/// <param name="ExpiresAt">When it stops being valid.</param>
/// <param name="Mode">How it is confirmed: typed, or held (typing the action text stays accepted).</param>
/// <param name="Channel">Where it was issued for.</param>
public sealed record IssuedChallenge(ChallengeId Id, ChallengeAction Action, ChallengeSubject Subject, DateTimeOffset ExpiresAt,
    ChallengeMode Mode = ChallengeMode.Typed, ChallengeChannel Channel = ChallengeChannel.Terminal)
{
    /// <summary>How long a challenge is valid: 5 minutes.</summary>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromMinutes(5);

    /// <summary>The shortest hold the app's button counts: 1.5 seconds.</summary>
    public static int MinHoldMs => 1500;

    /// <summary>How soon after issue an app answer may come: before this it is a slip, not a confirmation.</summary>
    public static TimeSpan EarliestAnswer { get; } = TimeSpan.FromSeconds(2);

    /// <summary>When the challenge was issued.</summary>
    public DateTimeOffset IssuedAt => ExpiresAt - Lifetime;
}
