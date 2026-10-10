using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetConfig;

internal sealed class GetConfigService(IJudgeConfigStore configs) : IService<GetConfigRequest, GetConfigResponse>
{
    public async ValueTask<Result<GetConfigResponse>> Handle(GetConfigRequest request, CancellationToken cancellationToken)
    {
        var loaded = await configs.Load(cancellationToken).ConfigureAwait(false);
        return loaded.IsError ? loaded.Error : new GetConfigResponse(loaded.Value);
    }
}
