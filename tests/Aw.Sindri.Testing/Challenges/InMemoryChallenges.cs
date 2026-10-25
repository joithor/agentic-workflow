using Aw.Kernel;
using Aw.Sindri.Core.Challenges;

namespace Aw.Sindri.Testing.Challenges;

/// <summary>
/// The server's challenges in memory (engine spec §4.2; <see cref="ChallengesContract"/> keeps it honest): one use, valid
/// for <see cref="IssuedChallenge.Lifetime"/> on <paramref name="clock"/>, bound to the action and the subject.
/// </summary>
/// <param name="clock">The clock expiry is read from.</param>
public sealed class InMemoryChallenges(TimeProvider clock) : IChallenges
{
    private readonly Dictionary<ChallengeId, IssuedChallenge> _open = [];
    private readonly Lock _gate = new();
    private int _next;

    /// <inheritdoc/>
    public IssuedChallenge Issue(ChallengeAction action, ChallengeSubject subject) => Issue(action, subject, ChallengeMode.Typed, ChallengeChannel.Terminal);

    /// <inheritdoc/>
    public IssuedChallenge Issue(ChallengeAction action, ChallengeSubject subject, ChallengeMode mode, ChallengeChannel channel)
    {
        lock (_gate)
        {
            var id = ChallengeId.For(new Guid(++_next, 0, 0, new byte[8]));
            var issued = new IssuedChallenge(id, action, subject, clock.GetUtcNow() + IssuedChallenge.Lifetime, mode, channel);
            foreach (var gone in _open.Values.Where(c => c.Action == action && c.Channel == channel).Select(static c => c.Id).ToList())
            {
                _ = _open.Remove(gone);
            }

            _open[id] = issued;
            return issued;
        }
    }

    /// <inheritdoc/>
    public Result<IssuedChallenge> Redeem(ChallengeAnswer answer, ChallengeAction expected, ChallengeSubject current, ErrorDefinition stale)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(stale);
        lock (_gate)
        {
            if (_open.TryGetValue(answer.Challenge, out var open) && clock.GetUtcNow() < open.ExpiresAt && ChallengeConfirmation.Refusal(open, answer, clock.GetUtcNow()) is { } unconfirmed)
            {
                return unconfirmed;
            }

            if (!_open.Remove(answer.Challenge, out var issued))
            {
                return stale.With("That challenge is unknown or was already used; run it again");
            }

            var why = Refusal(issued, answer, expected, current);
            return why is null ? issued : stale.With($"The challenge {why}; run it again and re-read the preview");
        }
    }

    private string? Refusal(IssuedChallenge issued, ChallengeAnswer answer, ChallengeAction expected, ChallengeSubject current) => true switch
    {
        _ when clock.GetUtcNow() >= issued.ExpiresAt => "expired",
        _ when issued.Action != expected => $"was for '{issued.Action.Text}', not '{expected.Text}'",
        _ when answer.HeldMs is null && !string.Equals(answer.Typed.Trim(), issued.Action.Text, StringComparison.Ordinal) => $"needed '{issued.Action.Text}' typed exactly",
        _ when issued.Subject != current => "has a subject that changed since the preview",
        _ => null,
    };
}
