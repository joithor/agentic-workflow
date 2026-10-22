using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;
using Aw.Sindri.Core.Locks;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>The code index's <see cref="IHeavyJobLock"/> over Sindri's box-wide heavy-job slots, so an index build counts against the same cap as builds, evidence runs and the shell tools.</summary>
/// <param name="slots">The slots.</param>
internal sealed class SindriCodeIndexHeavyLock(IHeavySlots slots) : IHeavyJobLock
{
    public async ValueTask<Result<IAsyncDisposable>> Take(string holder, TimeSpan maxWait, IProgress<string>? waiting, CancellationToken cancellationToken)
    {
        var taken = await slots.Take(holder, maxWait, waiting, cancellationToken).ConfigureAwait(false);
        if (taken.IsError)
        {
            return taken.Error;
        }

        return taken.Value.Lease is { } lease
            ? Results.Success(lease)
            : CodeIndexErrors.HeavyBusy.With("every heavy-job slot stayed busy: " + taken.Value.Busy);
    }
}
