using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;
/// <summary>Where a scoping run's surfaces stand, through the engine.</summary>
internal sealed class GetContextProposalsService : IService<GetContextProposalsRequest, ProposalsView>
{
    public ValueTask<Result<ProposalsView>> Handle(GetContextProposalsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
