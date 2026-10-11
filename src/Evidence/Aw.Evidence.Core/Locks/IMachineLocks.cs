using Aw.Kernel;

namespace Aw.Evidence.Core.Locks;

/// <summary>Box-wide locks shared with every other process on this machine (the shell tools take the same ones).</summary>
public interface IMachineLocks
{
    /// <summary>Takes <paramref name="kind"/>, waiting up to <paramref name="timeout"/> while another holder has it.</summary>
    /// <param name="kind">The lock.</param>
    /// <param name="timeout">How long to wait; zero tries once.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The lease, or AW-EVIDENCE-009 when it stayed busy.</returns>
    public ValueTask<Result<ILockLease>> AcquireAsync(LockKind kind, TimeSpan timeout, CancellationToken cancellationToken);
}
