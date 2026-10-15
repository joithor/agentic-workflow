using Aw.Context.Core;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The labeler a host without a labeling pass gets: a profile that turns 'context.labeling' on with nothing set up is SND-CONTEXT-042.</summary>
internal sealed class DisabledLabeler : ILabeler
{
    public ValueTask<Result<IReadOnlyList<ClusterLabel>>> Label(IReadOnlyList<FileCluster> clusters, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<ClusterLabel>>>(
            ContextErrors.LabelingUnavailable.With("labeling is on in the profile ('context.labeling.enabled') but this host has no model to ask (fix: set labeling up, or turn 'context.labeling.enabled' off)"));
}
