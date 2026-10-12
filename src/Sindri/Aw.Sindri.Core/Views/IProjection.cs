using Aw.Kernel;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>A fold over ledger events into view rows (API spec §2.3). An Error (or a throw) stalls the projector at that event (SND-VIEW-001).</summary>
public interface IProjection
{
    /// <summary>The view tables it writes; a rebuild empties them.</summary>
    public IReadOnlyList<string> Views { get; }

    /// <summary>True when it folds events of <paramref name="eventType"/>.</summary>
    /// <param name="eventType">The event type.</param>
    /// <returns>Whether it handles the type.</returns>
    public bool Handles(string eventType);

    /// <summary>Folds <paramref name="recorded"/> into <paramref name="rows"/>.</summary>
    /// <param name="recorded">The event.</param>
    /// <param name="rows">The open page transaction.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the error that stalls the projector.</returns>
    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken);
}
