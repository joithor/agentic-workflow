using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One map node through the engine, in the current caller's scope.</summary>
internal sealed class ShowContextNodeService(ContextScopes scopes, IContextEngine engine) : IService<ShowContextNodeRequest, NodeView>
{
    public async ValueTask<Result<NodeView>> Handle(ShowContextNodeRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.ShowNode(request.Key, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
