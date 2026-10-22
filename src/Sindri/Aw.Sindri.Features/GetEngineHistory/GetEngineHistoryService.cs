using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.EngineReads;

namespace Aw.Sindri.Features.GetEngineHistory;

internal sealed class GetEngineHistoryService(IEngineReader engine, IMirroredSequences mirrored) : IService<GetEngineHistoryRequest, EngineHistory>
{
    public ValueTask<Result<EngineHistory>> Handle(GetEngineHistoryRequest request, CancellationToken cancellationToken)
    {
        _ = (engine, mirrored);
        throw new NotSupportedException();
    }
}
