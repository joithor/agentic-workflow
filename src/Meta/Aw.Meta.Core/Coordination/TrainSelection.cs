using Aw.Kernel;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Chooses the pull requests of a train: the ones named, else the merge queue in train order.</summary>
/// <param name="config">The team config.</param>
/// <param name="queue">Reads the merge queue.</param>
public sealed class TrainSelection(IMetaConfig config, MergeQueueReader queue)
{
    /// <summary>The pull requests to gate.</summary>
    /// <param name="named">The pull requests named, or empty for the merge queue.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The team config and the candidates, or the error that kept either from loading.</returns>
    public async ValueTask<Result<TrainCandidates>> Choose(IReadOnlyList<int> named, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(named);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError || named.Count > 0)
        {
            return team.IsError ? team.Error : new TrainCandidates(team.Value, [.. named.Distinct()]);
        }

        var queued = await queue.Read(cancellationToken).ConfigureAwait(false);
        return queued.IsError ? queued.Error : new TrainCandidates(team.Value, TrainOrder.Sort([.. queued.Value.Select(q => q.Pr)], team.Value.MergeOrder));
    }
}
