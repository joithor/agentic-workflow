using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>What one refresh of a snapshot source changed.</summary>
/// <param name="Source">The source.</param>
/// <param name="Repos">Per repo.</param>
public sealed record RefreshReport(SourceId Source, IReadOnlyList<RepoRefresh> Repos);
