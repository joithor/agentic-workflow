using Aw.CodeIndex.Core.Model;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// The embeddings layer's model (spec §5.6). An implementation must talk only to a loopback address (never 'localhost'),
/// with no proxy and no redirect, refuse a cloud model, check the model's digest against the pin, and never download.
/// </summary>
public interface IEmbedder
{
    /// <summary>The name of the model <see cref="Embed"/> uses for these settings (the settings' model, else the pinned one); a layer's stamp and every stored vector carry it.</summary>
    /// <param name="settings">The embeddings settings.</param>
    /// <returns>The model name.</returns>
    public string ModelFor(EmbeddingsSettings settings);

    /// <summary>The check a build does before it embeds anything, with no work.</summary>
    /// <param name="settings">The embeddings settings.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>Success, or SND-CODEIDX-005 (bad URL or cloud model), 006 (server failed) or 009 (model missing or not the pinned digest).</returns>
    public ValueTask<Result<Unit>> Probe(EmbeddingsSettings settings, CancellationToken cancellationToken);

    /// <summary>Embeds texts, in order.</summary>
    /// <param name="settings">The embeddings settings.</param>
    /// <param name="texts">The texts, already scrubbed and cut; one batch.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>One vector per text; SND-CODEIDX-005 or 006.</returns>
    public ValueTask<Result<IReadOnlyList<ReadOnlyMemory<float>>>> Embed(EmbeddingsSettings settings, IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
