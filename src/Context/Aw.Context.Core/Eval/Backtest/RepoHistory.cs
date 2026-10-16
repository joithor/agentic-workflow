using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>A repo's first-parent history, oldest first.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Commits">The commits.</param>
public sealed record RepoHistory(ContextRepoName Repo, IReadOnlyList<HistoryCommit> Commits);
