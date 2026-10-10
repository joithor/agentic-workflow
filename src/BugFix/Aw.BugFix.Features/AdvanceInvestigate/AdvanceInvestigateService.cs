using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.AdvanceInvestigate;

internal sealed class AdvanceInvestigateService : IService<AdvanceInvestigateRequest, AdvanceInvestigateResponse>
{
    public ValueTask<Result<AdvanceInvestigateResponse>> Handle(AdvanceInvestigateRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("AdvanceInvestigate: make AdvanceInvestigateTests pass");
}
