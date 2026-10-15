using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Extract;

/// <summary>Turns clusters into candidate features (S-4): model-provenance feature nodes with 'implements' edges from their files.</summary>
public static class ClusterFeatures
{
    /// <summary>Proposes a feature per cluster.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="clusters">What the cluster source found.</param>
    /// <param name="labels">Names the labeling pass gave some clusters, or none.</param>
    /// <param name="extracted">The deterministic extraction: a cluster claims the surfaces whose defining file it holds ('part-of').</param>
    /// <returns>The features, their file links and their claimed surfaces.</returns>
    public static ExtractedMap Propose(ContextRepoName repo, ClusterSet clusters, IReadOnlyList<ClusterLabel> labels, ExtractedMap extracted)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(extracted);
        _ = repo;
        throw new NotSupportedException("S-4: ClusterFeatures.Propose is not built yet");
    }
}
