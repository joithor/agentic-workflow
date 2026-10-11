using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.PruneDecisionDetails;

internal sealed class PruneDecisionDetailsService : IService<PruneDecisionDetailsRequest, PruneDecisionDetailsResponse>
{
    public ValueTask<Result<PruneDecisionDetailsResponse>> Handle(PruneDecisionDetailsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: PruneDecisionDetails");
}
