using System.Globalization;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Features.ApplyCommand;

/// <summary>
/// The action class and the §4.2 challenge of a command (engine spec §4.2, §6.2): '--grant n', '--grant-tokens n' and
/// '--accept' are protected and need a challenge whose action text is 'grant n &lt;address&gt;', 'grant-tokens n
/// &lt;address&gt;' or 'accept &lt;address&gt;'; every other answer, a recipe command and a cancel are desk. The challenge
/// is bound to the instance as the preview showed it: its address, state, run and since.
/// </summary>
public static class ApplyCommandChallenge
{
    /// <summary>Whether the command is protected (W22).</summary>
    /// <param name="kind">The command's kind.</param>
    /// <param name="answer">The answer's variant, for an answer.</param>
    /// <returns>True for a grant, a token grant or an accept.</returns>
    public static bool IsProtected(ApplyCommandKind kind, AnswerVariant? answer) =>
        kind == ApplyCommandKind.Answer && answer is AnswerVariant.Grant or AnswerVariant.GrantTokens or AnswerVariant.Accept;

    /// <summary>The action text the human types back for a protected answer.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="answer">The variant.</param>
    /// <param name="amount">The grant's amount.</param>
    /// <returns>'grant 1 T-1/step/1', 'grant-tokens 100 T-1/step/1' or 'accept T-1/step/1'.</returns>
    public static ChallengeAction Action(InstanceAddress address, AnswerVariant answer, int? amount)
    {
        var n = amount?.ToString(CultureInfo.InvariantCulture) ?? "?";
        return new ChallengeAction(answer switch
        {
            AnswerVariant.Grant => $"grant {n} {address.Value}",
            AnswerVariant.GrantTokens => $"grant-tokens {n} {address.Value}",
            AnswerVariant.Accept => "accept " + address.Value,
            AnswerVariant.Text or AnswerVariant.Reject or AnswerVariant.Resume or _ =>
                throw new ArgumentOutOfRangeException(nameof(answer), answer, "only a grant, a token grant or an accept is protected"),
        });
    }

    /// <summary>What a protected answer's challenge is bound to: the instance as the preview showed it.</summary>
    /// <param name="view">The instance.</param>
    /// <returns>'instance:&lt;address&gt;@&lt;state&gt;:&lt;run&gt;:&lt;since&gt;'.</returns>
    public static ChallengeSubject Subject(InstanceView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new ChallengeSubject(
            string.Create(CultureInfo.InvariantCulture, $"instance:{view.Address.Value}@{view.State}:{view.Run?.Value}:{view.Since.ToUnixTimeMilliseconds()}"),
            Digest: null);
    }
}
