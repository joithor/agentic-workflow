using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The path between two map nodes through the engine, in the current caller's scope.</summary>
internal sealed class FindContextPathService(ContextScopes scopes, IContextEngine engine) : IService<FindContextPathRequest, PathView>
{
    public async ValueTask<Result<PathView>> Handle(FindContextPathRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.Path(request.From, request.To, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
