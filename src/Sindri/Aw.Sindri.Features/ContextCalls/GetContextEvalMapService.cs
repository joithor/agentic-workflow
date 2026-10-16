using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The map's quality on the owner's decisions, through the engine (which refuses a session with SND-CONTEXT-020).</summary>
internal sealed class GetContextEvalMapService(ContextScopes scopes, IContextEngine engine) : IService<GetContextEvalMapRequest, MapQualityRun>
{
    public async ValueTask<Result<MapQualityRun>> Handle(GetContextEvalMapRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.EvalMap(new MapQualityRequest(scope.Value), cancellationToken).ConfigureAwait(false);
    }
}
