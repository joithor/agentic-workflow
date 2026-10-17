namespace Aw.Context.Core.Eval.Backtest;

/// <summary>One first-parent commit of a repo's default branch.</summary>
/// <param name="Sha">The commit.</param>
/// <param name="At">Its committer date.</param>
/// <param name="Subject">Its subject line (a merge's title carries the issue key).</param>
/// <param name="Branch">The merged branch's name for a merge commit, or null.</param>
/// <param name="IsMerge">True for a merge commit.</param>
/// <param name="ChangedFiles">For a merge: the files changed against its first parent. Empty for other commits.</param>
public sealed record HistoryCommit(string Sha, DateTimeOffset At, string Subject, string? Branch, bool IsMerge, IReadOnlyList<string> ChangedFiles);
