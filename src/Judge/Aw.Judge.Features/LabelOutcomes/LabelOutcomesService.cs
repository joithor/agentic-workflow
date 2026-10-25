using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.LabelOutcomes;

internal sealed class LabelOutcomesService : IService<LabelOutcomesRequest, LabelOutcomesResponse>
{
    public ValueTask<Result<LabelOutcomesResponse>> Handle(LabelOutcomesRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("LabelOutcomes: make LabelOutcomesTests pass (cutover unit 6.3)");
}
