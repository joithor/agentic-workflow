namespace Aw.CodeIndex.Core.Model;

/// <summary>One regular file in a commit's tree, kept by the snapshot (spec §5.1).</summary>
/// <param name="Path">The repo-relative path, validated: no empty, '..', '.git', backslash, NUL or absolute component.</param>
/// <param name="Blob">The git object id of its content; "changed" is a different blob, so it costs no hashing.</param>
/// <param name="Size">The content size in bytes, from ls-tree.</param>
public sealed record SnapshotFile(string Path, string Blob, long Size);
