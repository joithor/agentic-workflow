using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.PruneDecisionDetails;

internal sealed class PruneDecisionDetailsService(IDecisionLog decisions, TimeProvider clock)
    : IService<PruneDecisionDetailsRequest, PruneDecisionDetailsResponse>
{
    public async ValueTask<Result<PruneDecisionDetailsResponse>> Handle(PruneDecisionDetailsRequest request, CancellationToken cancellationToken)
    {
        var pruned = await decisions.PruneDetailsBefore(clock.GetUtcNow() - DecisionDetails.Retention, cancellationToken).ConfigureAwait(false);
        return pruned.IsError ? pruned.Error : new PruneDecisionDetailsResponse(pruned.Value);
    }
}
