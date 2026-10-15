using System.Text.Json.Serialization;
namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>One state of the recipe as the band draws it (E-20).</summary>
/// <param name="State">The state's name (wire name 'id').</param>
/// <param name="Order">The 0-based position in path order for a main-path state; null for a branch state.</param>
/// <param name="BranchOf">The state this one is folded under, for a branch.</param>
/// <param name="Glyph">Done, active or pending.</param>
/// <param name="Badge">The badge on the state the instance is interrupted in.</param>
/// <param name="Round">The round counter for a state in a loop.</param>
/// <param name="Loops">The loop pairs this state belongs to.</param>
/// <param name="Desk">Whether it is a desk state.</param>
/// <param name="Child">Whether it waits on children.</param>
/// <param name="Children">Progress of a children state.</param>
/// <param name="Outcome">A recorded outcome worth showing on the band.</param>
/// <param name="Terminal">Whether it is a final state.</param>
public sealed record StateCell(
    [property: JsonPropertyName("id")] string State,
    int? Order,
    string? BranchOf,
    StateGlyph Glyph,
    EngineBadge? Badge,
    RoundOf? Round,
    IReadOnlyList<LoopPair> Loops,
    bool Desk,
    bool Child,
    ChildProgress? Children,
    string? Outcome,
    bool Terminal);
