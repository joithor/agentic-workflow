namespace Aw.Context.Core.Map;

/// <summary>A written export.</summary>
/// <param name="Path">The file.</param>
/// <param name="Version">The version it holds.</param>
/// <param name="Counts">What it holds.</param>
public sealed record MapExported(string Path, int Version, MapCounts Counts);
