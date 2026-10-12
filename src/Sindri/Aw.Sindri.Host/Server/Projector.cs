using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Host.Server;

/// <summary>Folds the ledger into views.db (API spec §2.3).</summary>
internal sealed class Projector
{
    public Projector(ILedgerReader ledger, IViewStore store, IEnumerable<IProjection> projections, IProjectorFeed feed, ICallerContextAccessor callers,
        TimeProvider clock, ILogger<Projector> log)
    {
        _ = (ledger, store, projections, feed, callers, clock, log);
    }

    /// <summary>One pass.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How it ended.</returns>
    public ValueTask<ProjectorPass> CatchUp(CancellationToken cancellationToken) => throw new NotSupportedException("red: unit B");
}
