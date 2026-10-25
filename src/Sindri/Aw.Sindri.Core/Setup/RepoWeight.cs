namespace Aw.Sindri.Core.Setup;

/// <summary>How much Sindri would read of a checkout (setup-once spec §5.2 "Preview").</summary>
/// <param name="Path">The checkout's real path (links followed).</param>
/// <param name="Files">How many files git tracks.</param>
/// <param name="Bytes">The size of the tracked files that exist on disk.</param>
public sealed record RepoWeight(string Path, int Files, long Bytes);
