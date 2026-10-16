using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Trust;

namespace Aw.Context.Features.QueryContext;

/// <summary>A catalog that answers the sources it was given, whatever the profile says.</summary>
internal sealed class FixedSources(IReadOnlyList<IContextSource> sources) : ISourceCatalog
{
    public IReadOnlyList<IContextSource> For(ContextSettings settings, IReadOnlyList<ContextRepo> repos, Scrubbers scrubbers) => sources;
}
