using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>What a build returns: the job's result (spec §3). Counts and words only, never a name, path or text.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Commit">The commit that was published, or null when nothing was (skipped).</param>
/// <param name="Outcome">Published or not-on-main.</param>
/// <param name="Counts">Files, symbols and edges of the published index; zero when none.</param>
/// <param name="Layers">Each layer's status.</param>
/// <param name="UnresolvedShare">The share of call sites that resolved to nothing.</param>
/// <param name="AmbiguousShare">The share that stayed ambiguous.</param>
/// <param name="Embedded">Symbols with a vector.</param>
/// <param name="Embeddable">Symbols that should have one; 'N of M embedded' when they differ.</param>
/// <param name="Incremental">True when it started from the live index.</param>
/// <param name="Milliseconds">How long it ran.</param>
public sealed record BuildReport(
    RepoName Repo,
    CommitSha? Commit,
    BuildOutcome Outcome,
    IndexCounts Counts,
    IReadOnlyList<LayerStatus> Layers,
    double UnresolvedShare,
    double AmbiguousShare,
    int Embedded,
    int Embeddable,
    bool Incremental,
    long Milliseconds);
