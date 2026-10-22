using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.ListEngineInstances;

internal sealed class ListEngineInstancesService(IEngineReader engine, TimeProvider clock) : IService<ListEngineInstancesRequest, EngineInstanceList>
{
    public ValueTask<Result<EngineInstanceList>> Handle(ListEngineInstancesRequest request, CancellationToken cancellationToken)
    {
        _ = (engine, clock);
        throw new NotSupportedException();
    }
}
