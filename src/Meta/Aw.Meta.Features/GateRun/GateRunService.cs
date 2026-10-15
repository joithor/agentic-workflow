using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.GateRun;

internal sealed class GateRunService : IService<GateRunRequest, GateRunResponse>
{
    public ValueTask<Result<GateRunResponse>> Handle(GateRunRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
