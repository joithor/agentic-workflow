using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.GetEngine;

internal sealed class GetEngineService(IEngineReader engine, TimeProvider clock) : IService<GetEngineRequest, EngineView>
{
    public ValueTask<Result<EngineView>> Handle(GetEngineRequest request, CancellationToken cancellationToken)
    {
        _ = (engine, clock);
        throw new NotSupportedException();
    }
}
