using Aw.CodeIndex.Core.Facade;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>The answer to GET /sindri/code-index/status: one row per repo in the caller's scope, from the index files only (stale, building and waiting are overlaid by later slices).</summary>
/// <param name="Repos">The repos' indexes.</param>
public sealed record CodeIndexStatusView(IReadOnlyList<RepoStatus> Repos);
