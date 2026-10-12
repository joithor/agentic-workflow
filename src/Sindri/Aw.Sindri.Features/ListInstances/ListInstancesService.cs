using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListInstances;

/// <summary>Red: engine P4 unit (e) builds it.</summary>
internal sealed class ListInstancesService : IService<ListInstancesRequest, ListInstancesResponse>
{
    public ValueTask<Result<ListInstancesResponse>> Handle(ListInstancesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("ListInstancesService: engine P4 unit (e) makes its tests pass");
}
