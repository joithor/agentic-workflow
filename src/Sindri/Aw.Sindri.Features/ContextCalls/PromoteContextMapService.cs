using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Promotes the latest draft (context spec §8.7), a protected command (README X30, X63): a draft with undecided review items, or no
/// draft, is SND-CONTEXT-046 before anything is asked; otherwise the preview names the draft and a confirmed draft is promoted and 'context.map.promoted' is appended.
/// </summary>
internal sealed class PromoteContextMapService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events)
    : IService<PromoteContextMapRequest, PromoteContextMapResponse>
{
    public async ValueTask<Result<PromoteContextMapResponse>> Handle(PromoteContextMapRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var ready = await Ready(scope.Value, cancellationToken).ConfigureAwait(false);
        if (ready.IsError)
        {
            return ready.Error;
        }

        var draft = ready.Value;
        if (request.Draft is null)
        {
            return new PromoteContextMapResponse(draft, Promoted: null, Sequence: null);
        }

        if (!string.Equals(request.Draft, draft, StringComparison.Ordinal))
        {
            return ContextErrors.PromoteRefused.With($"the latest draft is {draft}, not {request.Draft}, which you confirmed (fix: run 'sindri context map promote' again)");
        }

        return await Promote(draft, scope.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The latest draft's label when nothing in it is left to decide; else SND-CONTEXT-046 (a missing draft too).</summary>
    private async ValueTask<Result<string>> Ready(ContextScope scope, CancellationToken cancellationToken)
    {
        var queue = await engine.ReviewQueue(limit: null, scope, cancellationToken).ConfigureAwait(false);
        var ready = queue.IsError && queue.Error.Code == ContextErrors.NoDraftMap.Code ? PromoteReadiness.Check(queue: null)
            : queue.IsError ? queue.Error
            : PromoteReadiness.Check(queue.Value);
        return ready.IsError ? ready.Error : ready.Value.Draft;
    }

    private async ValueTask<Result<PromoteContextMapResponse>> Promote(string draft, ContextScope scope, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var promoted = await engine.Promote(scope, cancellationToken).ConfigureAwait(false);
        if (promoted.IsError)
        {
            return promoted.Error;
        }

        var appended = await new ContextMapEvents(ledger, events, grant).Promoted(promoted.Value, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new PromoteContextMapResponse(draft, promoted.Value, appended.Value.Sequence);
    }
}
