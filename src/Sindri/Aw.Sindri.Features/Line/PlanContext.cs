using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// What the plan checks read (line spec §6.2, §6.3), parsed once. 'plan' is PKT's task plan: phases[] with id, workstream, deps, size, kind, reason,
/// waitsForMerge, acceptance[] (id, evidence[] as kinds or {kind}; a hidden scenario has holdout: true and no text) and, optionally, maxPhaseSize,
/// maxStack and holdoutMin (the profile's workflow.plan.maxPhaseSize, workflow.line.maxStack, workflow.prove.holdoutMin; defaults M, 4 and 2) and
/// execReview[] (phase, verdict: executable | objection). 'design' supplies the live acceptance items and the signals (item, kind, runs).
/// 'holdoutSets' is [{phase, count, hash, canary}] (ids and hashes, never text).
/// </summary>
internal sealed class PlanContext(JsonNode? design, JsonNode? plan, JsonNode? holdoutSets)
{
    /// <summary>The phases.</summary>
    internal IReadOnlyList<JsonNode> Phases { get; } = [.. LineJson.Items(plan, "phases")];

    /// <summary>The plan fact.</summary>
    internal JsonNode? Plan { get; } = plan;

    /// <summary>The design's signals.</summary>
    internal IReadOnlyList<JsonNode> Signals { get; } = [.. LineJson.Items(design, "signals")];

    /// <summary>The live acceptance item ids of the design: not superseded.</summary>
    internal IReadOnlyList<string> LiveItems { get; } = [.. LineJson.Items(LineJson.Obj(design, "acceptance"), "items")
        .Where(static i => !string.Equals(LineJson.Str(i, "status"), "superseded", StringComparison.Ordinal))
        .Select(static i => LineJson.Str(i, "id")).OfType<string>()];

    /// <summary>The holdout sets.</summary>
    internal IReadOnlyList<JsonNode> HoldoutSets { get; } = [.. LineJson.Items(holdoutSets)];

    /// <summary>The phase's id.</summary>
    internal static string Id(JsonNode phase) => LineJson.Str(phase, "id") ?? "?";

    /// <summary>The visible acceptance entries of a phase (hidden scenarios are not items).</summary>
    internal static IEnumerable<JsonNode> Items(JsonNode phase) => LineJson.Items(phase, "acceptance").Where(static a => LineJson.Bool(a, "holdout") != true);
}
