namespace Aw.Context.Core.History;

/// <summary>What <see cref="IFileHistory"/> is asked: which paths, from which commit, inside which window.</summary>
/// <param name="UpTo">A commit sha that bounds the read from above, or null for HEAD.</param>
/// <param name="Paths">The repo-relative paths, matched literally (never a glob). Empty gives no changes.</param>
/// <param name="Window">The committer-date window, both ends inclusive.</param>
/// <param name="Max">The most changes to return.</param>
public sealed record FileHistoryQuery(string? UpTo, IReadOnlyList<string> Paths, HistoryWindow Window, int Max);
