using System.Text.Json.Nodes;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// What the design checks read (line spec §4.3, §4.4), parsed once. 'scope' is the loaded scope packet: scopeMap.surfaces[].id, acceptance.items[] (id, text),
/// openQuestions[].id. 'design' holds sections[] (id, covers, cites, assumes, answers), coverage.excluded[] (surface, reason), decisions[] (id, affects, cites,
/// options[]), deferred[] (question, owner, buildMayAssume), signals[] (item, kind, statement, runs, reference), contracts[] (lane, target, quote, row),
/// acceptance.items[] (the live list: scope's items unchanged, added ones with cites, superseded ones with supersededBy), resolutions[] (finding, text),
/// escapes[] (kind, id). 'lensRounds' is the round log: each round has selected[] (lens ids), writerModel and lenses[] (lens, state, model, findings[] with
/// id, severity); 'accepted' lists the finding ids the owner accepted as risks; 'style' is the pinned style pack, when the Step has one.
/// </summary>
internal sealed class DesignContext(JsonNode? scope, JsonNode? design, JsonNode? lensRounds, JsonNode? accepted, StylePack? style = null)
{
    /// <summary>The style pack spec.md is checked against; null when the caller has none (DSN-STYLE-001 then warns).</summary>
    internal StylePack? Style { get; } = style;

    /// <summary>The scope's surface ids.</summary>
    internal IReadOnlyList<string> SurfaceIds { get; } = [.. LineJson.Items(LineJson.Obj(scope, "scopeMap"), "surfaces").Select(static s => LineJson.Str(s, "id")).OfType<string>()];

    /// <summary>The scope's acceptance items.</summary>
    internal IReadOnlyList<JsonNode> ScopeItems { get; } = [.. LineJson.Items(LineJson.Obj(scope, "acceptance"), "items")];

    /// <summary>The scope's open question ids.</summary>
    internal IReadOnlyList<string> OpenQuestionIds { get; } = [.. LineJson.Items(scope, "openQuestions").Select(static q => LineJson.Str(q, "id")).OfType<string>()];

    /// <summary>The scope's answer log ids (A1, A2 …).</summary>
    internal IReadOnlyList<string> AnswerIds { get; } = [.. LineJson.Items(scope, "answerLog").Select(static a => LineJson.Str(a, "id")).OfType<string>()];

    /// <summary>The design's sections.</summary>
    internal IReadOnlyList<JsonNode> Sections { get; } = [.. LineJson.Items(design, "sections")];

    /// <summary>The design's open decisions.</summary>
    internal IReadOnlyList<JsonNode> Decisions { get; } = [.. LineJson.Items(design, "decisions")];

    /// <summary>The design's acceptance items: its own list when it keeps one, else the scope's.</summary>
    internal IReadOnlyList<JsonNode> Items { get; } = LineJson.Obj(design, "acceptance") is { } own ? [.. LineJson.Items(own, "items")] : [.. LineJson.Items(LineJson.Obj(scope, "acceptance"), "items")];

    /// <summary>The design fact.</summary>
    internal JsonNode? Design { get; } = design;

    /// <summary>The lens round log.</summary>
    internal IReadOnlyList<JsonNode> Rounds { get; } = [.. LineJson.Items(lensRounds)];

    /// <summary>The ids of the findings the owner accepted as risks.</summary>
    internal IReadOnlySet<string> Accepted { get; } = LineJson.Items(accepted).Select(static a => LineJson.Str(a, "id") ?? (a is JsonValue v && v.TryGetValue<string>(out var s) ? s : null)).OfType<string>().ToHashSet(StringComparer.Ordinal);

    /// <summary>The live acceptance items: not superseded.</summary>
    internal IEnumerable<JsonNode> Live => Items.Where(static i => !string.Equals(LineJson.Str(i, "status"), "superseded", StringComparison.Ordinal));

    /// <summary>Whether an id is a section or decision of the design.</summary>
    internal bool IsDesignId(string id) =>
        Sections.Any(s => string.Equals(LineJson.Str(s, "id"), id, StringComparison.Ordinal)) || Decisions.Any(d => string.Equals(LineJson.Str(d, "id"), id, StringComparison.Ordinal));

    internal static LineFinding Fail(string rule, string? item, string message, string fix) => new(rule, item, message, fix);

    internal static LineFinding Warning(string rule, string? item, string message, string fix) => new(rule, item, message, fix, Warn: true);
}
