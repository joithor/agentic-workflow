namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// 'workflow answer' (spec §5.8): pops the suspended frame its variant fits and runs that frame's resume action,
/// under this answer's own key. An answer that doesn't fit the reason is SND-WF-003, whose fix names the right one.
/// </summary>
/// <param name="Actor">The caller (a terminal-class human for the protected variants).</param>
/// <param name="Stamp">The nonce and argument digest.</param>
/// <param name="Variant">Which option was given.</param>
/// <param name="Text">The answer's text (scrubbed, at most 1 MiB), for '--text', '--file' or '--resume --text'.</param>
/// <param name="Amount">The rounds of '--grant' or the tokens of '--grant-tokens'.</param>
/// <param name="Forwarded">Set when the answer was given to a parent and forwarded to this child.</param>
public sealed record AnswerGiven(
    string Actor,
    CommandStamp Stamp,
    AnswerVariant Variant,
    string? Text,
    int? Amount,
    OnBehalfOf? Forwarded) : Trigger(Actor)
{
    /// <inheritdoc/>
    public override string Name => TriggerNames.Answer;

    /// <inheritdoc/>
    public override string KeyPart => Stamp.KeyPart(TriggerNames.Answer);
}
