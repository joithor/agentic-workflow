using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>One backtest sample: an issue and the merge that closed it (spec §9.1).</summary>
/// <param name="IssueKey">The tracker key.</param>
/// <param name="RecordRef">The issue record's reference.</param>
/// <param name="Version">The record's version, or null.</param>
/// <param name="CreatedAt">When the issue was created.</param>
/// <param name="Leaky">True when the brief was edited after the cut.</param>
/// <param name="Repo">The repo merged into.</param>
/// <param name="Merge">The merge commit.</param>
/// <param name="Cut">The last first-parent commit at or before <paramref name="CreatedAt"/>.</param>
/// <param name="TruthFiles">The files the merge changed, minus the deny globs.</param>
public sealed record CorpusSample(string IssueKey, string RecordRef, string? Version, DateTimeOffset CreatedAt, bool Leaky, ContextRepoName Repo, string Merge, string Cut,
    IReadOnlyList<string> TruthFiles);
