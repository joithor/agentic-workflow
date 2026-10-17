namespace Aw.Packets.Core.Kinds.Line;

/// <summary>
/// The line kinds' rule ids (LN-PKT1), one prefix per kind, numbered from 001. 001 to 006 are shared
/// (<see cref="LineRuleSet"/>); the numbers below are each kind's own. The ids are never PKT-SCP-* (scoping) or the
/// build kind's PKT-SCOPE-001/002 (write scopes).
/// </summary>
public static class LineRules
{
    /// <summary>Design packet: PKT-DSN-*.</summary>
    public static LineRuleSet Design { get; } = new("PKT-DSN");

    /// <summary>Plan graph: PKT-PLN-*.</summary>
    public static LineRuleSet Plan { get; } = new("PKT-PLN");

    /// <summary>Phase packet: PKT-PHS-*.</summary>
    public static LineRuleSet Phase { get; } = new("PKT-PHS");

    /// <summary>Proof pack: PKT-PRF-*.</summary>
    public static LineRuleSet Proof { get; } = new("PKT-PRF");

    /// <summary>Review result: PKT-REV-*.</summary>
    public static LineRuleSet Review { get; } = new("PKT-REV");

    /// <summary>PR hand-off: PKT-HND-*.</summary>
    public static LineRuleSet Handoff { get; } = new("PKT-HND");

    /// <summary>Design 007 / proof 007: acceptance.json matches its schema (008 is its chain rules); phase uses <see cref="PhaseAcceptanceSchema"/>.</summary>
    public const int AcceptanceSchema = 7;

    /// <summary>Plan 007: tasks.json matches the task-plan schema (the build kind's), so PKT's task rules run on it.</summary>
    public const int TasksSchema = 7;

    /// <summary>Phase 007: the packet id is '&lt;line&gt;-&lt;phase&gt;[-a&lt;n&gt;]'.</summary>
    public const int PhaseId = 7;

    /// <summary>Phase 008: phase.json matches its schema.</summary>
    public const int PhaseSchema = 8;

    /// <summary>Phase 009: phase.json has 'amends' exactly when the id ends '-a&lt;n&gt;', and an amendment's paths are allowed paths.</summary>
    public const int PhaseAmends = 9;

    /// <summary>Phase 010: acceptance.json matches its schema (011 is its chain rules).</summary>
    public const int PhaseAcceptanceSchema = 10;

    /// <summary>Phase 012: holdout.json matches its schema.</summary>
    public const int PhaseHoldout = 12;

    /// <summary>Handoff 007: pr.json matches its schema.</summary>
    public const int PullRequestSchema = 7;

    /// <summary>The line rule set of a kind; null for a kind that is not a line kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The rule set, or null.</returns>
    public static LineRuleSet? For(PacketKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        return kind.Name switch
        {
            "design" => Design,
            "plan" => Plan,
            "phase" => Phase,
            "proof" => Proof,
            "review" => Review,
            "handoff" => Handoff,
            _ => null,
        };
    }
}
