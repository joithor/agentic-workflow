namespace Aw.Context.Core.Map;

/// <summary>An imported map, now current.</summary>
/// <param name="Version">The new version.</param>
/// <param name="Counts">What it holds.</param>
/// <param name="Hash">The content hash that was confirmed.</param>
public sealed record MapImported(MapVersion Version, MapCounts Counts, string Hash);
