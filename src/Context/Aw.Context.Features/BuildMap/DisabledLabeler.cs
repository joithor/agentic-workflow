using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Features.BuildMap;

/// <summary>The labeler a host without a labeling pass gets: a profile that turns 'context.labeling' on with nothing set up is SND-CONTEXT-042. (S-4 stub: red until its slice is built.)</summary>
internal sealed class DisabledLabeler : ILabeler
{
    public ValueTask<Result<IReadOnlyList<ClusterLabel>>> Label(IReadOnlyList<FileCluster> clusters, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: DisabledLabeler is not built yet");
}
