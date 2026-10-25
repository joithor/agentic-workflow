namespace Aw.Sindri.Ui.Api.Context;

/// <summary>The map's counts.</summary>
/// <param name="Nodes">Nodes, as served.</param>
/// <param name="Edges">Edges, as served.</param>
/// <param name="Aliases">Aliases, as served.</param>
/// <param name="Decisions">Decisions, as served.</param>
/// <param name="Overrides">Overrides, as served.</param>
internal sealed record MapCounts(
    int Nodes,
    int Edges,
    int Aliases,
    int Decisions,
    int Overrides);
