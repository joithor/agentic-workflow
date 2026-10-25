namespace Aw.Context.Core.History;

/// <summary>The landed changes of a set of paths in one repo, newest first.</summary>
/// <param name="Head">The full sha of the checkout's HEAD.</param>
/// <param name="Shallow">True when the clone is shallow (older history may be missing).</param>
/// <param name="Changes">The changes inside the window, at most the requested number.</param>
/// <param name="Truncated">True when more changes matched than were returned.</param>
public sealed record FileHistory(string Head, bool Shallow, IReadOnlyList<FileChange> Changes, bool Truncated);
