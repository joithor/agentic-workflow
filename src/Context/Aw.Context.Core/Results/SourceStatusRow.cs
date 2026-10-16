using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>One source's state, for one repo where the source is per repo.</summary>
/// <param name="Source">The source.</param>
/// <param name="Kind">The layer it feeds.</param>
/// <param name="Repo">The repo, or null for a source that isn't per repo.</param>
/// <param name="State">Its state.</param>
/// <param name="Detail">One line: what was checked, never record text.</param>
/// <param name="Code">The problem's catalogue code, or null.</param>
/// <param name="LastSync">When it was last brought up to date, or null when never.</param>
/// <param name="Records">How many records (repo-docs: chunks) it holds or reaches.</param>
public sealed record SourceStatusRow(SourceId Source, SourceKind Kind, ContextRepoName? Repo, SourceState State, string Detail, string? Code,
    DateTimeOffset? LastSync, int Records)
{
    /// <summary>The last sync of an intent source (null for a source that doesn't sync).</summary>
    public SourceSyncStatus? Sync { get; init; }
}
