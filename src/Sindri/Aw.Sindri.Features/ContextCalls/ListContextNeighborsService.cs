using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>A node's neighbors through the engine, in the current caller's scope.</summary>
internal sealed class ListContextNeighborsService(ContextScopes scopes, IContextEngine engine) : IService<ListContextNeighborsRequest, NeighborsView>
{
    public async ValueTask<Result<NeighborsView>> Handle(ListContextNeighborsRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.Neighbors(request.Key, request.Depth, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
