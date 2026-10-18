using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The lenses of one anchor, through the engine (which gates and scopes sessions); a not-synced source row also names the sync job that is filling it.</summary>
internal sealed class GetContextLensesService(ContextScopes scopes, IContextEngine engine, SyncGaps gaps) : IService<GetContextLensesRequest, LensesView>
{
    public async ValueTask<Result<LensesView>> Handle(GetContextLensesRequest request, CancellationToken cancellationToken)
    {
        var read = MapRead.Of(request.Draft, request.MapVersion);
        if (read.IsError)
        {
            return read.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var view = await engine.Lenses(request.Anchor, read.Value, new LensRange(request.Depth, request.Hops), scope.Value, cancellationToken).ConfigureAwait(false);
        return view.IsError ? view.Error : view.Value with { Searched = await gaps.Mark(view.Value.Searched, cancellationToken).ConfigureAwait(false) };
    }
}
