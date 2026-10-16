namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One child instance row.</summary>
/// <param name="Id">Id, as served.</param>
/// <param name="Address">Address, as served.</param>
/// <param name="ItemId">ItemId, as served.</param>
/// <param name="Title">Title, as served.</param>
/// <param name="Glyph">Glyph, as served.</param>
/// <param name="State">State, as served.</param>
/// <param name="Chip">Chip, as served.</param>
/// <param name="Round">Round, as served.</param>
/// <param name="Agent">Agent, as served.</param>
/// <param name="AgeSeconds">AgeSeconds, as served.</param>
/// <param name="EngineOwned">EngineOwned, as served.</param>
/// <param name="MirroredPark">MirroredPark, as served.</param>
/// <param name="Actions">Actions, as served.</param>
internal sealed record ChildRow(
    string Id,
    string Address,
    string? ItemId,
    string Title,
    StateGlyph Glyph,
    string State,
    StateChip Chip,
    Round? Round,
    string? Agent,
    long AgeSeconds,
    EngineOwnedBadge? EngineOwned,
    EngineOwnedBadge? MirroredPark,
    IReadOnlyList<string> Actions);
