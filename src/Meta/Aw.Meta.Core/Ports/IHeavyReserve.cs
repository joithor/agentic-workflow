using Aw.Kernel;
using Aw.Meta.Core.Health;

namespace Aw.Meta.Core.Ports;

/// <summary>
/// The heavy-slot reservation this team's meta computes, kept in the team's own state directory (heavy-reserve). aw-meta never
/// writes the heavy-job wrapper's files: the meta runs meta-gate-reserve with the printed queue depth before each gate.
/// </summary>
public interface IHeavyReserve
{
    /// <summary>Reads the last computed reservation.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record, or null when none was computed yet or the file holds anything else.</returns>
    public ValueTask<Result<HeavyReserveRecord?>> Read(CancellationToken cancellationToken);

    /// <summary>Keeps a computation (a temporary file renamed over the real one).</summary>
    /// <param name="record">The reservation, the queue depth behind it and when it was computed.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or AW-META-010.</returns>
    public ValueTask<Result<Unit>> Write(HeavyReserveRecord record, CancellationToken cancellationToken);
}
