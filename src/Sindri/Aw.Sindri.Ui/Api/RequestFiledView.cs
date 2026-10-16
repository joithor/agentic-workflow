namespace Aw.Sindri.Ui.Api;

/// <summary>The answer to FileRequest: the accepted command and the ids it created.</summary>
/// <param name="Accepted">The accepted command; the next view query waits for its sequence.</param>
/// <param name="RequestId">The new request.</param>
/// <param name="DecisionId">The decision it opened.</param>
internal sealed record RequestFiledView(CommandAcceptedView Accepted, string RequestId, string DecisionId);
