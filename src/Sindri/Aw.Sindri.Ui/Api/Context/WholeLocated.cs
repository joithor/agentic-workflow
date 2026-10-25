namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Which area a located node is in.</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Area">Its area; null when the key is not in the map.</param>
/// <param name="Concept">Its concept's id ("other" when none claims it); null when the server does not say (proposed addition to the contract: the concept layer's search).</param>
internal sealed record WholeLocated(string Key, string? Area, string? Concept = null);
