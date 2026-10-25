using Aw.CodeIndex.Core.Facade;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>The answer to GET /sindri/code-index/status: one row per repo in the caller's scope, from the index files only (and its Health: stale, building and waiting laid over it).</summary>
/// <param name="Repos">The repos' indexes.</param>
public sealed record CodeIndexStatusView(IReadOnlyList<RepoStatus> Repos)
{
    /// <summary>One entry per repo in <see cref="Repos"/>: the Health card's state (stale, building, waiting) and the embeddings percent.</summary>
    public IReadOnlyList<RepoHealth> Health { get; init; } = [];
}
