using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetProposals;

/// <summary>Where each surface a scoping run proposed stands. (S-4 stub: red until its slice is built.)</summary>
internal sealed class GetProposalsService : IService<GetProposalsRequest, ProposalsView>
{
    public ValueTask<Result<ProposalsView>> Handle(GetProposalsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: GetProposals is not built yet");
}
