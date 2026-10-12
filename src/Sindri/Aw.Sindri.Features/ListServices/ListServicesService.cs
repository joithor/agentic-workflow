using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListServices;

internal sealed class ListServicesService : IService<ListServicesRequest, ListServicesResponse>
{
    public ValueTask<Result<ListServicesResponse>> Handle(ListServicesRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ListServices: make ListServicesTests pass");
}
