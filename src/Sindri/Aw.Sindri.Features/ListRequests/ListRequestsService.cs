using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListRequests;

internal sealed class ListRequestsService : IService<ListRequestsRequest, ListRequestsResponse>
{
    public ValueTask<Result<ListRequestsResponse>> Handle(ListRequestsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ListRequests: make ListRequestsTests pass");
}
