namespace Aw.CodeIndex.Core.Model;

/// <summary>A file row of the live index, what the planner compares the snapshot with (spec §5.2).</summary>
/// <param name="Path">The repo-relative path.</param>
/// <param name="Blob">The git object id the row was built from.</param>
/// <param name="Parsed">Whether a parser read it.</param>
public sealed record StoredFile(string Path, string Blob, bool Parsed);
