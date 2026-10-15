using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.GetEventCatalog;

/// <summary>Lists the catalog's public types, sorted by type, each with its key shape and the API views the app re-queries when it arrives (UI spec E-15).</summary>
internal sealed class GetEventCatalogService : IService<GetEventCatalogRequest, EventCatalogView>
{
    public ValueTask<Result<EventCatalogView>> Handle(GetEventCatalogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var types = EventCatalog.All
            .Where(static e => e.Public)
            .OrderBy(static e => e.Type, StringComparer.Ordinal)
            .Select(static e => new EventTypeView(e.Type, e.KeyShape, e.Invalidates))
            .ToList();
        return ValueTask.FromResult<Result<EventCatalogView>>(new EventCatalogView(types));
    }
}
