using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>The single-slot class of the setup service's automatic jobs (setup-once spec §5).</summary>
/// <param name="running">How many jobs the runner has now.</param>
#pragma warning disable MA0182 // red stub: the service that uses it comes with the implementation
internal sealed class SetupJobQueue(Func<int> running)
{
    private readonly List<(JobKind Kind, JobKey Key)> _waiting = [];

    /// <summary>The jobs waiting, in the order they will start.</summary>
    internal IReadOnlyList<(JobKind Kind, JobKey Key)> Waiting => running() >= 0 ? _waiting : [];

    internal ValueTask<Result<SetupJobOutcome>> Submit(JobKind kind, JobKey key, Func<CancellationToken, ValueTask<Result<Unit>>> start, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    internal ValueTask Finished(JobKind kind, JobKey key, CancellationToken cancellationToken) => throw new NotSupportedException();

    internal ValueTask Pump(CancellationToken cancellationToken) => throw new NotSupportedException();
}
