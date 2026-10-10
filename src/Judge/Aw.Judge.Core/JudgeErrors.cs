using Aw.Kernel;

namespace Aw.Judge.Core;

/// <summary>The Judge error catalogue (codes AW-JUDGE-NNN).</summary>
public static class JudgeErrors
{
    /// <summary>AW-JUDGE-001.</summary>
    public static readonly ErrorDefinition Invalid = new("AW-JUDGE-001", ErrorKind.Invalid, "Judge input failed validation");

    /// <summary>AW-JUDGE-002: no decision has this id ('why', 'undo', 'approve').</summary>
    public static readonly ErrorDefinition UnknownDecision = new("AW-JUDGE-002", ErrorKind.NotFound, "unknown decision");

    /// <summary>AW-JUDGE-003: a decision with this id is already recorded.</summary>
    public static readonly ErrorDefinition DecisionExists = new("AW-JUDGE-003", ErrorKind.Conflict, "decision already recorded");

    /// <summary>AW-JUDGE-004: a brief for this tool use id is already saved.</summary>
    public static readonly ErrorDefinition BriefExists = new("AW-JUDGE-004", ErrorKind.Conflict, "brief already saved");

    /// <summary>AW-JUDGE-010: the --providers flag or AW_JUDGE_PROVIDERS is malformed; the message is the TS error text.</summary>
    public static readonly ErrorDefinition ProvidersFlag = new("AW-JUDGE-010", ErrorKind.Invalid, "bad providers allowlist");

    /// <summary>Every Judge error, for ErrorCatalogue.Validate.</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } = [Invalid, UnknownDecision, DecisionExists, BriefExists, ProvidersFlag];
}
