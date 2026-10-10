using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetConfig;

internal sealed class GetConfigService : IService<GetConfigRequest, GetConfigResponse>
{
    public ValueTask<Result<GetConfigResponse>> Handle(GetConfigRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetConfig: make GetConfigTests pass");
}
