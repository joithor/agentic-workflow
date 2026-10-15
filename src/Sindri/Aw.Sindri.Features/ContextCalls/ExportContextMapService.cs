using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Writes the current map to the exports folder, through the engine (a session can't: SND-CONTEXT-020).</summary>
internal sealed class ExportContextMapService(ContextScopes scopes, IContextEngine engine) : IService<ExportContextMapRequest, MapExported>
{
    public async ValueTask<Result<MapExported>> Handle(ExportContextMapRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.ExportMap(request.Path, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
