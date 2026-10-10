using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ApproveDecision;

internal sealed class ApproveDecisionService(IDecisionLog decisions) : IService<ApproveDecisionRequest, ApproveDecisionResponse>
{
    // Marks an escalated decision as cleared by the user (TS runApprove), until approvals get their own table.
    private const string ApprovedPrefix = "approved:";

    public async ValueTask<Result<ApproveDecisionResponse>> Handle(ApproveDecisionRequest request, CancellationToken cancellationToken)
    {
        var approved = await decisions.PrefixReasonCode(request.DecisionId, ApprovedPrefix, cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var found = await decisions.Find(request.DecisionId, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : new ApproveDecisionResponse(found.Value);
    }
}
