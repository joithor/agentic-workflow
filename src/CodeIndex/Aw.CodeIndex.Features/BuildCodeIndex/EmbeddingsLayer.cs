using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>The embeddings layer of a build (spec §5.6, §5.7): probes the model, embeds the symbols with stored text in batches and records the layer's row.</summary>
/// <param name="embedder">The embedding model.</param>
/// <param name="clock">The time source.</param>
public sealed class EmbeddingsLayer(IEmbedder embedder, TimeProvider clock)
{
    /// <summary>Runs the layer and records its row; a model that fails leaves the layer unavailable with the failure's code, never a failed build.</summary>
    /// <param name="build">The build file.</param>
    /// <param name="settings">The embeddings settings.</param>
    /// <param name="progress">Told a counts-only line per batch.</param>
    /// <param name="cancellationToken">Cancels the layer between batches.</param>
    /// <returns>How many symbols have a vector and how many should; an error only when the build file cannot be read or written.</returns>
    public ValueTask<Result<EmbeddingProgress>> Run(IIndexBuild build, EmbeddingsSettings settings, IProgress<string> progress, CancellationToken cancellationToken)
    {
        _ = (embedder, clock);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(progress);
        throw new NotSupportedException();
    }
}
