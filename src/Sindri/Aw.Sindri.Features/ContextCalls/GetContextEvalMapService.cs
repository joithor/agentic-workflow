using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Stub.</summary>
internal sealed class GetContextEvalMapService(ContextScopes scopes, IContextEngine engine) : IService<GetContextEvalMapRequest, MapQualityRun>
{
    public ValueTask<Result<MapQualityRun>> Handle(GetContextEvalMapRequest request, CancellationToken cancellationToken)
    {
        _ = (scopes, engine);
        throw new NotSupportedException();
    }
}
