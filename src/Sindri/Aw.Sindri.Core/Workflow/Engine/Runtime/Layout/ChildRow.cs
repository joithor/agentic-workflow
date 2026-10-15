namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>One child instance (E-20).</summary>
/// <param name="Address">The child's address.</param>
/// <param name="ItemId">Its item id.</param>
/// <param name="Title">The title.</param>
/// <param name="Glyph">Done, active or pending.</param>
/// <param name="State">Its state.</param>
/// <param name="Round">Its round counter.</param>
/// <param name="Agent">The agent working on it.</param>
/// <param name="AgeSeconds">Whole seconds since it entered the state.</param>
/// <param name="EngineOwned">The badge when the engine interrupted it.</param>
/// <param name="MirroredPark">The park it mirrors.</param>
/// <param name="Actions">The action ids allowed now.</param>
public sealed record ChildRow(
    string Address,
    string? ItemId,
    string Title,
    StateGlyph Glyph,
    string State,
    RoundOf? Round,
    string? Agent,
    long AgeSeconds,
    EngineBadge? EngineOwned,
    ChildPark? MirroredPark,
    IReadOnlyList<string> Actions);
