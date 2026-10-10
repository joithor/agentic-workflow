using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetConfig;

internal sealed class SetConfigService : IService<SetConfigRequest, SetConfigResponse>
{
    public ValueTask<Result<SetConfigResponse>> Handle(SetConfigRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("SetConfig: make SetConfigTests pass");
}
