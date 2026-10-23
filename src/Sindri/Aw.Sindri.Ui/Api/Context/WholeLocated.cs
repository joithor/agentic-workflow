namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Which area a located node is in.</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Area">Its area; null when the key is not in the map.</param>
internal sealed record WholeLocated(string Key, string? Area);
