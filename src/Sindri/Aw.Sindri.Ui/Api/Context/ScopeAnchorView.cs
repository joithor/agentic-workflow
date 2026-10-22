namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One starting point of an item's frozen scope, with the map version it was read at.</summary>
/// <param name="Key">The anchor's node key.</param>
/// <param name="MapVersion">The map version the scoping read it at ('v7').</param>
internal sealed record ScopeAnchorView(string Key, string MapVersion);
