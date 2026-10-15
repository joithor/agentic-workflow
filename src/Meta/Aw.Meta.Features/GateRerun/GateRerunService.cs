using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.GateRerun;

internal sealed class GateRerunService : IService<GateRerunRequest, GateRerunResponse>
{
    public ValueTask<Result<GateRerunResponse>> Handle(GateRerunRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
