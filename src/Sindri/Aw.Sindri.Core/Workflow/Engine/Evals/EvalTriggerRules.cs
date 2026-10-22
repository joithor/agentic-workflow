using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>The §9.2 rules a trigger line keeps: a known kind, a variant that agrees with it, 'park' and 'amount' only where they mean something.</summary>
internal static class EvalTriggerRules
{
    private static readonly string[] _kinds =
    [
        TriggerNames.StepCompleted, TriggerNames.StepFailed, TriggerNames.WaitSatisfied, TriggerNames.ChildParked, TriggerNames.ChildResumed,
        TriggerNames.ChildEscalated, TriggerNames.Answer, TriggerNames.Takeover, TriggerNames.Handback, TriggerNames.Cancel, TriggerNames.Deadline,
    ];

    /// <summary>What is wrong with <paramref name="line"/>, with its fix; null when it keeps the rules.</summary>
    public static string? Unfit(EvalTrigger line) => Kind(line) ?? Park(line) ?? Reason(line) ?? Tokens(line) ?? Variant(line) ?? Amount(line);

    /// <summary>The answer option a variant gives.</summary>
    public static AnswerVariant Answer(EvalVariant variant) => variant switch
    {
        EvalVariant.Grant => AnswerVariant.Grant,
        EvalVariant.GrantTokens => AnswerVariant.GrantTokens,
        EvalVariant.Accept => AnswerVariant.Accept,
        EvalVariant.Reject => AnswerVariant.Reject,
        EvalVariant.Text => AnswerVariant.Text,
        EvalVariant.Resume or EvalVariant.Takeover or EvalVariant.Handback or EvalVariant.Cancel or EvalVariant.Deadline or EvalVariant.Command or _ => AnswerVariant.Resume,
    };

    /// <summary>A variant as the line spells it.</summary>
    public static string Name(EvalVariant variant) => variant switch
    {
        EvalVariant.Grant => "grant",
        EvalVariant.GrantTokens => "grant-tokens",
        EvalVariant.Accept => "accept",
        EvalVariant.Reject => "reject",
        EvalVariant.Resume => "resume",
        EvalVariant.Text => "text",
        EvalVariant.Takeover => "takeover",
        EvalVariant.Handback => "handback",
        EvalVariant.Cancel => "cancel",
        EvalVariant.Deadline => "deadline",
        EvalVariant.Command or _ => "command",
    };

    /// <summary>The variant a kind other than 'answer' goes with; null for a recipe trigger or a child's change.</summary>
    public static EvalVariant? For(string kind) => kind.StartsWith(TriggerNames.CommandPrefix, StringComparison.Ordinal)
        ? EvalVariant.Command
        : kind switch
        {
            TriggerNames.Takeover => EvalVariant.Takeover,
            TriggerNames.Handback => EvalVariant.Handback,
            TriggerNames.Cancel => EvalVariant.Cancel,
            TriggerNames.Deadline => EvalVariant.Deadline,
            _ => null,
        };

    private static string? Kind(EvalTrigger line)
    {
        if (!line.Kind.StartsWith(TriggerNames.CommandPrefix, StringComparison.Ordinal))
        {
            return _kinds.Contains(line.Kind, StringComparer.Ordinal)
                ? null
                : $"'{line.Kind}' isn't a trigger an eval line can name (fix: use one of {string.Join(", ", _kinds)} or command.<verb>)";
        }

        return CommandVerb.TryFrom(line.Kind[TriggerNames.CommandPrefix.Length..], out _)
            ? null
            : $"'{line.Kind}' names no command verb (fix: command.<verb>, the verb [a-z][a-z-]{{1,31}})";
    }

    private static string? Reason(EvalTrigger line) => line.Reason switch
    {
        null => null,
        _ when line.Kind != TriggerNames.StepFailed => "'reason' belongs on a step.failed line only (fix: remove it, or use kind step.failed)",
        { Length: 0 } => "'reason' is empty (fix: say what failed, or leave 'reason' out for the default)",
        _ => null,
    };

    private static string? Tokens(EvalTrigger line) => line.Tokens switch
    {
        null => null,
        _ when line.Kind is not (TriggerNames.StepCompleted or TriggerNames.StepFailed) => "'tokens' belongs on a step.completed or step.failed line only (fix: remove it, or use one of those kinds)",
        < 0 => "'tokens' must be 0 or more (fix: the tokens the run reported)",
        _ => null,
    };

    private static string? Park(EvalTrigger line) => line.Park is not null && line.Kind != TriggerNames.ChildParked
        ? "'park' belongs on a child.parked line only (fix: remove it, or use kind child.parked)"
        : null;

    private static string? Variant(EvalTrigger line)
    {
        if (line.Kind == TriggerNames.Answer)
        {
            return line.Variant is EvalVariant.Grant or EvalVariant.GrantTokens or EvalVariant.Accept or EvalVariant.Reject or EvalVariant.Resume or EvalVariant.Text
                ? null
                : "an answer line needs 'variant' (fix: grant, grant-tokens, accept, reject, resume or text)";
        }

        var expected = For(line.Kind);
        if (line.Variant is not { } variant || variant == expected)
        {
            return null;
        }

        return $"variant '{Name(variant)}' doesn't go with kind '{line.Kind}' (fix: {(expected is { } e ? $"use variant '{Name(e)}' or leave it out" : "leave it out")})";
    }

    private static string? Amount(EvalTrigger line)
    {
        var grants = line.Variant is EvalVariant.Grant or EvalVariant.GrantTokens;
        if (grants)
        {
            return line.Amount is null ? $"variant '{Name(line.Variant!.Value)}' needs 'amount' (fix: the n of --grant n)" : null; // why: grants is true only with a variant
        }

        return line.Amount is null ? null : "'amount' belongs on a grant or grant-tokens answer only (fix: remove it)";
    }
}
