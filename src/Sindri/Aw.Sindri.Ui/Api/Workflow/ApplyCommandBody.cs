using System.Text.Json.Serialization;

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

/// <summary>A challenge's answer: its id and the text typed, or how long the button was held.</summary>
/// <param name="Challenge">The challenge id.</param>
/// <param name="Typed">The typed text; empty for a hold.</param>
/// <param name="HeldMs">Milliseconds held; left out when typed.</param>
internal sealed record ChallengeAnswerBody(
    string Challenge,
    string Typed = "",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? HeldMs = null);

/// <summary>What the API answers a command it applied (the result words and the nonce are in the body too; the app reads the instance again instead).</summary>
/// <param name="AlreadyApplied">True when the same command had already been applied, so nothing new happened.</param>
internal sealed record CommandApplied(bool AlreadyApplied);
