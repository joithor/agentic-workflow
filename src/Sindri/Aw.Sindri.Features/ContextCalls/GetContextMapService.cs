using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The current map version and its counts, through the engine (which gates and scopes sessions).</summary>
internal sealed class GetContextMapService(ContextScopes scopes, IContextEngine engine) : IService<GetContextMapRequest, MapStatusView>
{
    public async ValueTask<Result<MapStatusView>> Handle(GetContextMapRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.MapStatus(request.Anchor, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
