using Aw.Kernel;
using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Tests.AppConfirm;

/// <summary>Challenges that redeem nothing: every answer is the caller's stale error, as a missing or wrong confirmation is.</summary>
internal sealed class RefusingChallenges : IChallenges
{
    /// <summary>How many answers were offered.</summary>
    public int Redeemed { get; private set; }

    public IssuedChallenge Issue(ChallengeAction action, ChallengeSubject subject) => throw new NotSupportedException();

    public IssuedChallenge Issue(ChallengeAction action, ChallengeSubject subject, ChallengeMode mode, ChallengeChannel channel) => throw new NotSupportedException();

    public Result<IssuedChallenge> Redeem(ChallengeAnswer answer, ChallengeAction expected, ChallengeSubject current, ErrorDefinition stale)
    {
        ArgumentNullException.ThrowIfNull(stale);
        Redeemed++;
        return stale.With("the confirmation was refused");
    }
}
