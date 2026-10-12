using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>
/// A command on an instance (engine spec rev 8 §5.2, §5.8, §6.3 'POST /workflow/instances/{id}/commands'): 'workflow
/// answer' in its variants, 'workflow command &lt;verb&gt;' or 'workflow cancel', sent as the Update its kind names with
/// the nonce as its id.
/// </summary>
/// <param name="Instance">The instance's address, or a bare item for the root of its latest attempt.</param>
/// <param name="Kind">Answer, command or cancel.</param>
/// <param name="Answer">The answer's variant, for an answer.</param>
/// <param name="Text">The answer's text ('--text', '--file', '--resume --text'), at most 1 MiB.</param>
/// <param name="Amount">The rounds of '--grant n' or the tokens of '--grant-tokens n'.</param>
/// <param name="Verb">The recipe verb, for a command.</param>
/// <param name="Nonce">The command's nonce; null to have one issued.</param>
/// <param name="Challenge">The challenge and its typed text, for a protected answer (grant, grant-tokens, accept).</param>
/// <param name="Actor">Who sends it ('human:cli').</param>
public sealed record ApplyCommandRequest(
    string Instance,
    ApplyCommandKind Kind,
    AnswerVariant? Answer,
    string? Text,
    int? Amount,
    CommandVerb? Verb,
    CommandNonce? Nonce,
    ChallengeAnswer? Challenge,
    string Actor);
