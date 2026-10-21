namespace Aw.CodeIndex.Core.Model;

/// <summary>One entry of a commit's tree as git lists it, before <see cref="InputRules"/> decides what to keep (spec §5.1).</summary>
/// <param name="Mode">The git mode: 100644 and 100755 are regular files, 120000 a symlink, 160000 a submodule.</param>
/// <param name="Path">The full repo-relative path, '/'-separated, as git lists it (not yet validated).</param>
/// <param name="Blob">The git object id; for a submodule, the commit id in the other repo.</param>
/// <param name="Size">The content size in bytes, or null when the object is not in the store (a promisor clone's missing blob).</param>
public sealed record TreeEntry(string Mode, string Path, string Blob, long? Size);
