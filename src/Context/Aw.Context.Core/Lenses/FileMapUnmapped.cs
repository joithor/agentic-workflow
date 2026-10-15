namespace Aw.Context.Core.Lenses;

/// <summary>An entry nothing reaches.</summary>
/// <param name="Path">Its path.</param>
/// <param name="Files">Files under it.</param>
public sealed record FileMapUnmapped(string Path, int Files);
