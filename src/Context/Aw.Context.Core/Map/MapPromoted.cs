namespace Aw.Context.Core.Map;

/// <summary>A draft promoted: now the current version.</summary>
/// <param name="Version">The new current version.</param>
/// <param name="Retained">The version that was current and is now retained, or null when there was none.</param>
public sealed record MapPromoted(MapVersion Version, MapVersion? Retained);
