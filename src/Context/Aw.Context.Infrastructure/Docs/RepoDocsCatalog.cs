using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Store;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>The sources of Phase 1: one repo-docs source over the repos in scope (none when no repo is in scope).</summary>
internal sealed class RepoDocsCatalog(ContextDatabase database, GitCli git, TimeProvider clock) : ISourceCatalog
{
    public IReadOnlyList<IContextSource> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos) =>
        repos.Count == 0 ? [] : [new RepoDocsSource(repos, database, git, clock)];
}
