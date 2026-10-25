namespace Aw.Sindri.Core.Scoping;

/// <summary>Where one proposed surface stands on the context map.</summary>
/// <param name="Surface">The title the run gave it.</param>
/// <param name="Node">The node key it resolved to, or null.</param>
/// <param name="Status">'mapped', 'new' or 'unmapped'.</param>
public sealed record ScopeSurfaceStatus(string Surface, string? Node, string Status);
