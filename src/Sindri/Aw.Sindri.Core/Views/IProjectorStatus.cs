using Aw.Kernel;

namespace Aw.Sindri.Core.Views;

/// <summary>The projector's in-memory status (API spec §2.2): queries read the head and the cursor here, not from the ledger.</summary>
public interface IProjectorStatus
{
    /// <summary>The status now.</summary>
    public ViewsStatus Current { get; }

    /// <summary>Waits until the cursor reaches <paramref name="sequence"/>.</summary>
    /// <param name="sequence">The sequence to wait for.</param>
    /// <param name="limit">How long to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>Success, or SND-API-011 naming the cursor and the target past the limit, and at once while stalled.</returns>
    public ValueTask<Result<Unit>> WaitFor(long sequence, TimeSpan limit, CancellationToken cancellationToken);
}
