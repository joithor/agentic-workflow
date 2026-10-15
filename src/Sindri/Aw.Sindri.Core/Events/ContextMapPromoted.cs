namespace Aw.Sindri.Core.Events;

/// <summary>A draft became the current map (context spec §3 'context.map.promoted').</summary>
/// <param name="Version">The new current version's number.</param>
/// <param name="Repo">The map's repo.</param>
/// <param name="Retained">The version that was current before and is now retained, or null.</param>
public sealed record ContextMapPromoted(int Version, string Repo, int? Retained);
