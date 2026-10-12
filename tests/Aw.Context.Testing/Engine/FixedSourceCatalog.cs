using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;

namespace Aw.Context.Testing.Engine;

/// <summary>An <see cref="ISourceCatalog"/> that returns its sources for any scope with a repo in it (none for an empty scope), and records the repos it was asked for. It passes <see cref="SourceCatalogContract{TCatalog}"/>.</summary>
/// <param name="sources">The sources.</param>
public sealed class FixedSourceCatalog(params IContextSource[] sources) : ISourceCatalog
{
    /// <summary>The repos of the last call.</summary>
    public IReadOnlyList<ContextRepo> LastRepos { get; private set; } = [];

    /// <inheritdoc />
    public IReadOnlyList<IContextSource> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos)
    {
        LastRepos = repos;
        return repos.Count == 0 ? [] : sources;
    }
}
