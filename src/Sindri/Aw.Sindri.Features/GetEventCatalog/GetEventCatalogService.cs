using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetEventCatalog;

internal sealed class GetEventCatalogService : IService<GetEventCatalogRequest, EventCatalogView>
{
    public ValueTask<Result<EventCatalogView>> Handle(GetEventCatalogRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetEventCatalog: make GetEventCatalogTests pass");
}
