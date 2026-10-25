using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The history lens, through the engine (which gates and scopes sessions). The window is absolute and checked first (SND-CONTEXT-006), then the map read; a not-synced
/// intent source row also names the sync job that is filling it.
/// </summary>
internal sealed class GetContextHistoryService(ContextScopes scopes, IContextEngine engine, SyncGaps gaps) : IService<GetContextHistoryRequest, HistoryView>
{
    public async ValueTask<Result<HistoryView>> Handle(GetContextHistoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var window = HistoryWindow.Of(request.Since, request.Before);
        var read = MapRead.Of(request.Draft, request.MapVersion);
        if (window.IsError || read.IsError)
        {
            return window.IsError ? window.Error : read.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var view = await engine.History(request.Anchors, window.Value, read.Value, scope.Value, cancellationToken).ConfigureAwait(false);
        return view.IsError ? view.Error : view.Value with { Searched = await gaps.Mark(view.Value.Searched, cancellationToken).ConfigureAwait(false) };
    }
}
