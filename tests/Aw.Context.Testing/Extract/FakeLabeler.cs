using Aw.Context.Core.Extract;
using Aw.Context.Core.Trust;
using Aw.Kernel;

namespace Aw.Context.Testing.Extract;

/// <summary>An <see cref="ILabeler"/> that names every cluster 'Labeled &lt;id&gt;' (or fails with <see cref="Failure"/>) and records the clusters it was shown.</summary>
public sealed class FakeLabeler : ILabeler
{
    /// <summary>When set, every pass fails with it.</summary>
    public Error? Failure { get; set; }

    /// <summary>The clusters shown to the model, across passes.</summary>
    public IList<FileCluster> Shown { get; } = [];

    /// <summary>The extra scrub patterns each pass was given.</summary>
    public IList<IReadOnlyList<ScrubPattern>> Patterns { get; } = [];

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ClusterLabel>>> Label(IReadOnlyList<FileCluster> clusters, IReadOnlyList<ScrubPattern> extraPatterns, int maxTokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clusters);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<ClusterLabel>>>(failure);
        }

        Patterns.Add(extraPatterns);
        foreach (var cluster in clusters)
        {
            Shown.Add(cluster);
        }

        return ValueTask.FromResult(Results.Success<IReadOnlyList<ClusterLabel>>([.. clusters.Select(c => new ClusterLabel(c.Id, "Labeled " + c.Id.Value))]));
    }
}
