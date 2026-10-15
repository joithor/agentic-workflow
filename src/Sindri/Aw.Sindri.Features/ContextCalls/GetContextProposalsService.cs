using Aw.Context.Core.Facade;
using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Where a scoping run's surfaces stand, through the engine.</summary>
internal sealed class GetContextProposalsService(ContextScopes scopes, IContextEngine engine) : IService<GetContextProposalsRequest, ProposalsView>
{
    public async ValueTask<Result<ProposalsView>> Handle(GetContextProposalsRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.Proposals(request.Run, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
