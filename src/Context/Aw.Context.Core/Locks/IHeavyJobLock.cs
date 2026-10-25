using Aw.Kernel;

namespace Aw.Context.Core.Locks;

/// <summary>
/// The box-wide heavy-job lock a backtest takes (spec §8.6; the map build takes none): the box-wide slots (two by default) shared with the
/// shell helpers. Sindri supplies the adapter; the engine only asks for a slot.
/// </summary>
public interface IHeavyJobLock
{
    /// <summary>Takes a slot, waiting while another holder has them all.</summary>
    /// <param name="waiting">Told, once per change, who holds the slots ('waiting for heavy-job lock (slot 1/2 held by …)'), or null.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The held slot (disposing releases it), or SND-CONTEXT-040 naming the holder when it stayed busy.</returns>
    public ValueTask<Result<IAsyncDisposable>> Take(IProgress<string>? waiting, CancellationToken cancellationToken);
}
