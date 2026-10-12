using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>Red: engine P4 unit (e) builds it.</summary>
internal sealed class GetInstanceService : IService<GetInstanceRequest, GetInstanceResponse>
{
    public ValueTask<Result<GetInstanceResponse>> Handle(GetInstanceRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("GetInstanceService: engine P4 unit (e) makes its tests pass");
}
