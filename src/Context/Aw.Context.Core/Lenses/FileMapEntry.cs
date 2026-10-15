using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>One direct child of the path asked for.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Path">Its path in the repo, with a trailing '/' for a directory.</param>
/// <param name="Directory">True for a directory.</param>
/// <param name="Status">Worst of its own status or its children's.</param>
/// <param name="Files">Files under it (1 for a file).</param>
/// <param name="Features">The features the file (or any file under it) implements, at most 10.</param>
public sealed record FileMapEntry(string Name, string Path, bool Directory, FileMapStatus Status, int Files, IReadOnlyList<NodeKey> Features);
