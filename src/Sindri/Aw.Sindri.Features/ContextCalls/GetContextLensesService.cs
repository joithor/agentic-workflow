using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The lenses of one anchor, through the engine (which gates and scopes sessions).</summary>
internal sealed class GetContextLensesService(ContextScopes scopes, IContextEngine engine) : IService<GetContextLensesRequest, LensesView>
{
    public async ValueTask<Result<LensesView>> Handle(GetContextLensesRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.Lenses(request.Anchor, request.Draft, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
