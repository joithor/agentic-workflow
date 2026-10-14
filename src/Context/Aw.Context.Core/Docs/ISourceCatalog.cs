using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Docs;

/// <summary>Builds the sources a call may read: one per configured adapter, over the repos in the caller's scope.</summary>
public interface ISourceCatalog
{
    /// <summary>The sources for <paramref name="repos"/> under <paramref name="settings"/>.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="repos">The repos in scope (a session's repo only).</param>
    /// <returns>The sources, in a stable order.</returns>
    public IReadOnlyList<IContextSource> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos);
}
