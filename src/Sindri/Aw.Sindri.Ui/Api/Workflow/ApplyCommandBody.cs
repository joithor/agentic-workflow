namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>
/// The body of POST /sindri/workflow/instances/{id}/commands for a protected answer (engine spec §4.2, §6.3): '--grant-tokens n' with the
/// challenge the person answered by hold or by typing. The instance is its address; the route names it by id.
/// </summary>
/// <param name="Instance">The instance's address.</param>
/// <param name="Kind">'answer'.</param>
/// <param name="Answer">The answer's variant, 'grant-tokens'.</param>
/// <param name="Amount">The tokens granted.</param>
/// <param name="Challenge">The challenge and its answer.</param>
internal sealed record ApplyCommandBody(string Instance, string Kind, string Answer, int Amount, ChallengeAnswerBody Challenge)
{
    /// <summary>The token grant of <paramref name="amount"/> on <paramref name="address"/>.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="amount">The tokens.</param>
    /// <param name="challenge">The challenge and its answer.</param>
    /// <returns>The body.</returns>
    internal static ApplyCommandBody GrantTokens(string address, int amount, ChallengeAnswerBody challenge) => new(address, "answer", "grant-tokens", amount, challenge);
}
