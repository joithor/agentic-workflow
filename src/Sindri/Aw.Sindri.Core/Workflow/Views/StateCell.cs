using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One state of the recipe in path order (E-20).</summary>
/// <param name="State">The state id (wire name <c>id</c>). Text, because a state id has no typed id yet; the name stays <c>State</c> and the wire name comes from the attribute.</param>
/// <param name="Order">The 1-based position in path order on the main path; <c>null</c> for a branch state (WF section 3.10).</param>
/// <param name="BranchOf">The state this one is folded under.</param>
/// <param name="Glyph">The mark.</param>
/// <param name="Badge">The badge replacing the glyph.</param>
/// <param name="Round">The round, for a looped state.</param>
/// <param name="Loops">The loop pairs this state belongs to.</param>
/// <param name="Desk">Whether it is a desk state.</param>
/// <param name="Child">Whether it waits on children.</param>
/// <param name="Children">Children progress.</param>
/// <param name="Outcome">A recorded outcome worth showing.</param>
/// <param name="Terminal">Whether it is final.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StateCell(
    [property: JsonPropertyName("id")] string State,
    int? Order,
    string? BranchOf,
    StateGlyph Glyph,
    EngineOwnedBadge? Badge,
    Round? Round,
    IReadOnlyList<LoopPair> Loops,
    bool Desk,
    bool Child,
    Progress? Children,
    string? Outcome,
    bool Terminal);
