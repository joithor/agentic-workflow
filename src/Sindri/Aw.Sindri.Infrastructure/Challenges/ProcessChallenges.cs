using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Infrastructure.Challenges;

/// <summary>
/// The server's challenges, held in this process (engine spec §4.2, S1-D5): one use, valid for
/// <see cref="IssuedChallenge.Lifetime"/>, bound to the action and the subject. They die with the process, so nothing
/// can be confirmed from a preview the human didn't see. A singleton; expired challenges are dropped as new ones are issued.
/// </summary>
/// <param name="clock">The clock expiry is read from.</param>
/// <param name="ids">Mints the challenge ids.</param>
internal sealed class ProcessChallenges(TimeProvider clock, IIdGenerator ids) : IChallenges
{
    private readonly Dictionary<ChallengeId, IssuedChallenge> _open = [];
    private readonly Lock _gate = new();

    public IssuedChallenge Issue(ChallengeAction action, ChallengeSubject subject) => Issue(action, subject, ChallengeMode.Typed, ChallengeChannel.Terminal);

    public IssuedChallenge Issue(ChallengeAction action, ChallengeSubject subject, ChallengeMode mode, ChallengeChannel channel)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(subject);
        var now = clock.GetUtcNow();
        var issued = new IssuedChallenge(ChallengeId.For(ids.NewId()), action, subject, now + IssuedChallenge.Lifetime, mode, channel);
        lock (_gate)
        {
            // Expired challenges go, and so does an open one for this action on this channel: a new challenge replaces it (an open challenge on the other channel stays).
            foreach (var gone in _open.Values.Where(c => c.ExpiresAt <= now || (c.Action == action && c.Channel == channel)).Select(static c => c.Id).ToList())
            {
                _ = _open.Remove(gone);
            }

            _open[issued.Id] = issued;
        }

        return issued;
    }

    public Result<IssuedChallenge> Redeem(ChallengeAnswer answer, ChallengeAction expected, ChallengeSubject current, ErrorDefinition stale)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(stale);
        IssuedChallenge? issued;
        lock (_gate)
        {
            if (_open.TryGetValue(answer.Challenge, out issued) && clock.GetUtcNow() < issued.ExpiresAt && ChallengeConfirmation.Refusal(issued, answer, clock.GetUtcNow()) is { } unconfirmed)
            {
                return unconfirmed;   // not the owner's confirmation yet (a short hold, an early answer, the wrong kind): the challenge stays open
            }

            _ = _open.Remove(answer.Challenge, out issued);
        }

        if (issued is null)
        {
            return stale.With("that challenge is unknown or was already used (challenges die when sindri restarts); run it again");
        }

        var why = Refusal(issued, answer, expected, current);
        return why is null ? issued : stale.With($"the challenge {why}; nothing was recorded. Run it again and re-read the preview");
    }

    private string? Refusal(IssuedChallenge issued, ChallengeAnswer answer, ChallengeAction expected, ChallengeSubject current) => true switch
    {
        _ when clock.GetUtcNow() >= issued.ExpiresAt => "expired (a challenge lasts 5 minutes)",
        _ when issued.Action != expected => $"was for '{issued.Action.Text}', not '{expected.Text}'",
        _ when answer.HeldMs is null && !string.Equals(answer.Typed.Trim(), issued.Action.Text, StringComparison.Ordinal) => $"needed '{issued.Action.Text}' typed exactly",
        _ when issued.Subject != current => "was issued for a subject that changed since the preview",
        _ => null,
    };
}
