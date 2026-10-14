namespace Aw.Context.Core.Map;

/// <summary>One doctor check of the map ('context:map', 'context:retention').</summary>
/// <param name="Name">The check's name.</param>
/// <param name="State">Its outcome.</param>
/// <param name="Detail">What it found.</param>
/// <param name="Fix">What to do, or null when nothing.</param>
public sealed record MapHealth(string Name, MapHealthState State, string Detail, string? Fix);
