using Aw.Context.Core.Extract;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;

namespace Aw.Context.Features.QueryContext;

/// <summary>The query (spec §8.2) over the live map and the profile's sources: <see cref="QueryPipeline"/> with the store's map.</summary>
internal sealed class QueryContextService(ISourceCatalog catalog, IScrubberFactory scrubberFactory, IIdGenerator ids, TimeProvider clock, MapReader graphs, IIntentStore intents, IRepoReader repoReader)
    : IService<QueryContextRequest, ContextResult>
{
    /// <summary>The most fused items fetched (and so cited) per query.</summary>
    internal const int MaxItems = QueryPipeline.MaxItems;

    public ValueTask<Result<ContextResult>> Handle(QueryContextRequest request, CancellationToken cancellationToken) =>
        new QueryPipeline(scrubberFactory, ids, clock, intents).Run(request, new PipelineInputs(new LiveMap(graphs), catalog, Diff: new DiffAnswer(graphs, repoReader)), cancellationToken);
}
