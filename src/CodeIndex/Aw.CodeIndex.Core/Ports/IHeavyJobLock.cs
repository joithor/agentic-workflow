using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>The box-wide heavy-job slots a build takes (spec §5.7); Sindri supplies the adapter over IHeavySlots, the block only asks for a slot.</summary>
public interface IHeavyJobLock
{
    /// <summary>Takes a slot, waiting while other holders have them all.</summary>
    /// <param name="holder">Who holds it ('sindri code-index:&lt;repo&gt;').</param>
    /// <param name="maxWait">The longest wait; the build uses 10 minutes.</param>
    /// <param name="waiting">Told, once per change, who holds the slots, or null.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The held slot (disposing releases it), or SND-CODEIDX-004 when every slot stayed busy past <paramref name="maxWait"/>.</returns>
    public ValueTask<Result<IAsyncDisposable>> Take(string holder, TimeSpan maxWait, IProgress<string>? waiting, CancellationToken cancellationToken);
}
