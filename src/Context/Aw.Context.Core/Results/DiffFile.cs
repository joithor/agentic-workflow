namespace Aw.Context.Core.Results;

/// <summary>A changed file with its place in the map.</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Change">How it changed.</param>
/// <param name="Status">Mapped (a confirmed 'implements' edge), suggested (only unconfirmed ones) or unmapped, as 'filemap' says.</param>
/// <param name="Nodes">Every node the file implements, confirmed first, then by key.</param>
public sealed record DiffFile(string Path, ChangeKind Change, DiffStatus Status, IReadOnlyList<DiffNode> Nodes);
