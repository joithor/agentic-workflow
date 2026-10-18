using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The lenses of several anchors, read from one map snapshot, through the engine.</summary>
internal sealed class GetContextLensesSetService(ContextScopes scopes, IContextEngine engine) : IService<GetContextLensesSetRequest, LensesSetView>
{
    public async ValueTask<Result<LensesSetView>> Handle(GetContextLensesSetRequest request, CancellationToken cancellationToken)
    {
        var read = MapRead.Of(request.Draft, request.MapVersion);
        if (read.IsError)
        {
            return read.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.LensesOf(request.Anchors, read.Value, new LensRange(request.Depth, request.Hops), scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
