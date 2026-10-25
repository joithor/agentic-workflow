using Aw.Kernel;

namespace Aw.Sindri.Core.Challenges;

/// <summary>
/// The server's challenges (engine spec §4.2, S1-D5), shared by every protected action. They live in the server process
/// and die on restart, so nothing can be confirmed from a preview the human didn't see.
/// </summary>
public interface IChallenges
{
    /// <summary>Issues a one-time challenge for <paramref name="action"/> on <paramref name="subject"/>, valid for <see cref="IssuedChallenge.Lifetime"/>.</summary>
    /// <param name="action">The action text.</param>
    /// <param name="subject">What it binds.</param>
    /// <returns>The challenge.</returns>
    public IssuedChallenge Issue(ChallengeAction action, ChallengeSubject subject);

    /// <summary>
    /// Issues a one-time challenge for <paramref name="action"/> on <paramref name="subject"/> that is confirmed in <paramref name="mode"/> on
    /// <paramref name="channel"/>. It replaces an open challenge for the same action on the same channel (an open terminal challenge survives an app one).
    /// </summary>
    /// <param name="action">The action text.</param>
    /// <param name="subject">What it binds.</param>
    /// <param name="mode">Typed, or held (typing stays accepted).</param>
    /// <param name="channel">Where it is answered.</param>
    /// <returns>The challenge.</returns>
    public IssuedChallenge Issue(ChallengeAction action, ChallengeSubject subject, ChallengeMode mode, ChallengeChannel channel);

    /// <summary>
    /// Redeems <paramref name="answer"/> for a request whose own action is <paramref name="expected"/> and whose subject is
    /// now <paramref name="current"/>. Any attempt uses the challenge up.
    /// </summary>
    /// <param name="answer">The challenge id and the typed text.</param>
    /// <param name="expected">The action text the request's verb and arguments make.</param>
    /// <param name="current">The subject as it is now.</param>
    /// <param name="stale">The refusal: SND-RECIPE-009 for recipe challenges, SND-WF-013 for workflow ones.</param>
    /// <returns>
    /// The redeemed challenge; or <paramref name="stale"/> when it is unknown, used, expired, for another action, typed differently, or its subject changed;
    /// or SND-SETUP-003 (and the challenge stays open) when an app answer is the wrong kind, comes under 2 seconds after issue, or holds under the minimum.
    /// </returns>
    public Result<IssuedChallenge> Redeem(ChallengeAnswer answer, ChallengeAction expected, ChallengeSubject current, ErrorDefinition stale);
}
