using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The event bridge's cursor (engine spec rev 8 §5.4): the last ledger sequence it has delivered, one owner-only file
/// under the Sindri home ('workflow/events.cursor'). A missing file reads as 0, the start of the ledger.
/// </summary>
/// <param name="home">Where the file is.</param>
internal sealed class EventBridgeCursor(WorkflowsHome home)
{
    /// <summary>The last delivered sequence.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sequence, 0 when nothing was delivered yet.</returns>
    public ValueTask<Result<long>> Read(CancellationToken cancellationToken) =>
        throw new NotSupportedException($"engine P4 unit (d): the cursor at {home.EventsCursorFile}");

    /// <summary>Records <paramref name="sequence"/> as delivered.</summary>
    /// <param name="sequence">The last delivered sequence.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Done.</returns>
    public ValueTask<Result<Unit>> Advance(long sequence, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"engine P4 unit (d): the cursor at {home.EventsCursorFile}");
}
