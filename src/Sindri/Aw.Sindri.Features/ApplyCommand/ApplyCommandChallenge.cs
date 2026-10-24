using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>
/// The action class of a command (engine spec §4.2, §6.2): '--grant n', '--grant-tokens n' and '--accept' are protected
/// and need no typed challenge (owner decision q93); every other answer, a recipe
/// command and a cancel are desk.
/// </summary>
public static class ApplyCommandChallenge
{
    /// <summary>Whether the command is protected (W22).</summary>
    /// <param name="kind">The command's kind.</param>
    /// <param name="answer">The answer's variant, for an answer.</param>
    /// <returns>True for a grant, a token grant or an accept.</returns>
    public static bool IsProtected(ApplyCommandKind kind, AnswerVariant? answer) =>
        kind == ApplyCommandKind.Answer && answer is AnswerVariant.Grant or AnswerVariant.GrantTokens or AnswerVariant.Accept;
}
