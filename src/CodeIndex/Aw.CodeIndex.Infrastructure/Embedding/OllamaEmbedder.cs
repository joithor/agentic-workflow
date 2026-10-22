using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Embedding;

/// <summary>Ollama on the loopback address as the embeddings layer's model (spec §5.6).</summary>
internal sealed class OllamaEmbedder : IEmbedder
{
    /// <summary>The model and digest in context/code-index.lock.</summary>
    internal static readonly ModelPin DefaultPin = new("nomic-embed-text:latest", "sha256:0a109f422b47e3a30ba2b10eca18548e944e8a23073ee3f3e947efcf3c45e59f");

    public OllamaEmbedder()
    {
    }

    internal OllamaEmbedder(ModelPin pin, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(handler);
    }

    public string ModelFor(EmbeddingsSettings settings) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> Probe(EmbeddingsSettings settings, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> Embed(EmbeddingsSettings settings, IReadOnlyList<string> texts, CancellationToken cancellationToken) => throw new NotSupportedException();
}
