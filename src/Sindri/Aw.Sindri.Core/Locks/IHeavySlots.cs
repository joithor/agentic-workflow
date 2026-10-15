using Aw.Kernel;

namespace Aw.Sindri.Core.Locks;

/// <summary>
/// The box-wide heavy-job lock (builds, test runs, index builds, map builds): Sindri's own port over the shared lock
/// directory, so the context engine and the shell tools count against one cap (context spec §4 "The heavy-job lock is a
/// port, supplied by Sindri"). The workflow engine's 'heavy' allocator is meant to use it too; it is not wired yet.
/// </summary>
public interface IHeavySlots
{
    /// <summary>Takes a free slot, waiting until <paramref name="timeout"/> for one.</summary>
    /// <param name="holder">Who is taking it, written to the slot's owner file ('sindri context-map-build').</param>
    /// <param name="timeout">How long to wait for a slot.</param>
    /// <param name="waiting">Told one line when every slot is held ('waiting for heavy-job lock (slot 1/2 held by …)'), or null.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The taken slot, or a busy answer naming the holder; an error only when the lock directory can't be used.</returns>
    public ValueTask<Result<HeavySlotTaken>> Take(string holder, TimeSpan timeout, IProgress<string>? waiting, CancellationToken cancellationToken);
}
