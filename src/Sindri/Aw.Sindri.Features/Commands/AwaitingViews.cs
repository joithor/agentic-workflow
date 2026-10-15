using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.Commands;

/// <summary>
/// The chain's innermost decorator (API spec §2.1, plan G7): a command whose catalog row has ReadsViews waits, inside the
/// gate, until the views reach the ledger head before its handler decides. While they rebuild it is SND-API-010, while
/// they stall SND-VIEW-001, both at once; still behind after 2 s, SND-API-011. The handler isn't run on any of them.
/// Commands that read no views (StopProcess, RebuildViews) pass straight through.
/// </summary>
/// <param name="inner">The handler.</param>
/// <param name="catalog">The command catalog.</param>
/// <param name="views">The projector's progress.</param>
/// <param name="ledger">The ledger, for its head.</param>
/// <typeparam name="TRequest">The command record.</typeparam>
/// <typeparam name="TResponse">What it answers.</typeparam>
internal sealed class AwaitingViews<TRequest, TResponse>(IService<TRequest, TResponse> inner, ICommandCatalog catalog, IProjectorStatus views, ILedgerStore ledger)
    : Decorated<TRequest, TResponse>(inner)
    where TRequest : notnull
{
    /// <summary>How long a command waits for the views.</summary>
    internal static readonly TimeSpan Limit = TimeSpan.FromSeconds(2);

    public override async ValueTask<Result<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        if (!ActionClassifier.Classify(catalog, request).ReadsViews)
        {
            return await Inner.Handle(request, cancellationToken).ConfigureAwait(false);
        }

        var ready = await Ready(cancellationToken).ConfigureAwait(false);
        return ready.IsError ? ready.Error : await Inner.Handle(request, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Ready(CancellationToken cancellationToken)
    {
        if (Blocked() is { } blocked)
        {
            return blocked;
        }

        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var waited = await views.WaitFor(head.Value, Limit, cancellationToken).ConfigureAwait(false);
        return waited.IsError ? Blocked() ?? waited.Error : waited;   // a stall or rebuild that began during the wait is named, not SND-API-011
    }

    private Error? Blocked()
    {
        var status = views.Current;
        if (status.Stalled)
        {
            return SindriErrors.ViewsStalled.With(string.Create(CultureInfo.InvariantCulture,
                $"views stalled at {status.StalledAt} ({status.StalledType}); the command wasn't run (fix: sindri views rebuild)"));
        }

        return status.Rebuilding ? SindriErrors.ViewsRebuilding.With("views are rebuilding; the command wasn't run, retry in a moment") : null;
    }
}
