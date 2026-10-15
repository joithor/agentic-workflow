using Aw.Kernel;

namespace Aw.Context.Core.Extract;

/// <summary>Names clusters with a model (the optional labeling pass; off by default). Over its token cap it answers SND-CONTEXT-042.</summary>
public interface ILabeler
{
    /// <summary>Names the clusters.</summary>
    /// <param name="clusters">The clusters.</param>
    /// <param name="maxTokens">The most tokens (sent and received) the pass may spend ('context.labeling.maxTokens').</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>A label per cluster the model could name.</returns>
    public ValueTask<Result<IReadOnlyList<ClusterLabel>>> Label(IReadOnlyList<FileCluster> clusters, int maxTokens, CancellationToken cancellationToken);
}
