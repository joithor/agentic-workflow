using Aw.Context.Core.Docs;

namespace Aw.Context.Features.QueryContext;

/// <summary>What the pipeline answers from: the map it reads, the sources it searches and the time the sources are cut to (null: now).</summary>
/// <param name="Map">The map provider.</param>
/// <param name="Catalog">The source catalog.</param>
/// <param name="AsOf">The time every source is asked to cut to, or null for the live query.</param>
internal sealed record PipelineInputs(IMapProvider Map, ISourceCatalog Catalog, DateTimeOffset? AsOf = null);
