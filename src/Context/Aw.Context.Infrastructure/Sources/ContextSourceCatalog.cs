using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Git;
using Aw.Context.Infrastructure.Onyx;
using Aw.Context.Infrastructure.Store;

namespace Aw.Context.Infrastructure.Sources;

/// <summary>
/// The sources for one call, in a stable order: repo-docs over the repos in scope, then the onyx source when the profile
/// names 'context.sources.onyx' (built per call, as <see cref="OnyxSources"/> asks). No repo in scope gives no source at
/// all, onyx included (SourceCatalogContract): a caller whose scope maps no repo reads nothing. Which of the sources a
/// caller may read is ScopeRules.MayRead's decision, in the slices.
/// </summary>
internal sealed class ContextSourceCatalog(ContextDatabase database, GitCli git, TimeProvider clock, OnyxSources onyx) : ISourceCatalog
{
    public IReadOnlyList<IContextSource> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(repos);
        if (repos.Count == 0)
        {
            return [];
        }

        var sources = new List<IContextSource>(2) { new RepoDocsSource(repos, database, git, clock) };

        if (settings.Onyx is { } onyxSettings)
        {
            sources.Add(onyx.Create(onyxSettings, new HashSet<string>(settings.LinearTeams, StringComparer.Ordinal)));
        }

        return sources;
    }
}
