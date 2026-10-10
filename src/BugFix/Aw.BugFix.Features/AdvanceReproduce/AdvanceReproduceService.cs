using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.AdvanceReproduce;

internal sealed class AdvanceReproduceService : IService<AdvanceReproduceRequest, AdvanceReproduceResponse>
{
    public ValueTask<Result<AdvanceReproduceResponse>> Handle(AdvanceReproduceRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("AdvanceReproduce: make AdvanceReproduceTests pass");
}
