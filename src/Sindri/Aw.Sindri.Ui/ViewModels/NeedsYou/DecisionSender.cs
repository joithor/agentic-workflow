using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The one way the app sends Decide (the decision sheet and the item sheet share it): a decision is sent once and never retried
/// by the app. A press while the same decision is in flight does nothing; a refusal or a lost answer leaves the decision
/// pressable (the human may press again); only an accepted Decide marks it sent. The guard is by decision id, so another
/// decision shown meanwhile is neither blocked nor marked.
/// </summary>
internal sealed class DecisionSender(ISindriApi api)
{
    private readonly HashSet<string> _sent = new(StringComparer.Ordinal);
    private readonly HashSet<string> _inFlight = new(StringComparer.Ordinal);

    /// <summary>True when the decision may be sent now: not accepted yet and not in flight.</summary>
    /// <param name="decisionId">The decision.</param>
    /// <returns>True when a press would send.</returns>
    internal bool CanSend(string decisionId) => !_sent.Contains(decisionId) && !_inFlight.Contains(decisionId);

    /// <summary>True while a Decide for the decision is in flight.</summary>
    /// <param name="decisionId">The decision.</param>
    /// <returns>True while it is.</returns>
    internal bool IsInFlight(string decisionId) => _inFlight.Contains(decisionId);

    /// <summary>Sends Decide for a decision; the guard is read and set before the first await.</summary>
    /// <param name="url">The API.</param>
    /// <param name="decisionId">The decision.</param>
    /// <param name="optionId">The option chosen.</param>
    /// <param name="note">The note sent with it; null for none.</param>
    /// <returns>The answer, or null when the decision was not sendable (in flight or accepted).</returns>
    internal async Task<Result<CommandAcceptedView>?> Send(Uri url, string decisionId, string optionId, string? note)
    {
        if (!CanSend(decisionId))
        {
            return null;
        }

        _ = _inFlight.Add(decisionId);
        Result<CommandAcceptedView> answer;
        try
        {
            answer = await api.Decide(url, decisionId, new DecideRequest(optionId, note is { Length: > 0 } ? note : null), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            _ = _inFlight.Remove(decisionId);
        }

        if (answer.IsSuccess)
        {
            _ = _sent.Add(decisionId);
        }

        return answer;
    }
}
