using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The typed confirmation of the protected context commands (README X30), shared by promote and the protected review actions:
/// a preview issues the server's challenge, and the answer must redeem that challenge for the same action text and subject.
/// </summary>
/// <param name="challenges">The process's open challenges.</param>
internal sealed class ContextChallenges(IChallenges challenges)
{
    /// <summary>Issues a challenge for <paramref name="action"/> bound to <paramref name="subject"/>.</summary>
    /// <param name="action">The text the human types.</param>
    /// <param name="subject">What it is about: a change to the subject makes the challenge stale.</param>
    /// <returns>The challenge, as the preview shows it.</returns>
    internal ContextChallenge Issue(ChallengeAction action, ChallengeSubject subject)
    {
        var issued = challenges.Issue(action, subject);
        return new ContextChallenge(issued.Id, issued.Action.Text, issued.ExpiresAt);
    }

    /// <summary>Checks the human's answer: none, or one that doesn't redeem this challenge for this subject, is SND-AUTH-002.</summary>
    /// <param name="answer">The challenge id and the typed text, or null.</param>
    /// <param name="action">The action text expected.</param>
    /// <param name="subject">The subject as it is now.</param>
    /// <param name="cli">The command the human runs to try again, for the refusal's words.</param>
    /// <returns>Null when the answer redeems the challenge; else the refusal.</returns>
    internal Error? Refuse(ChallengeAnswer? answer, ChallengeAction action, ChallengeSubject subject, string cli) => answer is null
        ? SindriErrors.AuthRefused.With($"this needs you at an interactive terminal: run '{cli}' and type '{action.Text}'")
        : challenges.Redeem(answer, action, subject, SindriErrors.AuthRefused) is { IsError: true } stale ? stale.Error : null;
}
