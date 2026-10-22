using Aw.CodeIndex.Core.Model;
using Aw.Kernel;

namespace Aw.CodeIndex.Core.Embedding;

/// <summary>Where the embeddings layer may send text (spec §5.6): an IP-literal loopback address, never 'localhost', and never a cloud model.</summary>
public static class EmbeddingTarget
{
    /// <summary>Checks the URL of the settings.</summary>
    /// <param name="url">The embedding server's URL.</param>
    /// <returns>True for an http URL on 127.0.0.1 or [::1] with no credentials.</returns>
    public static bool IsLoopbackUrl(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        throw new NotSupportedException();
    }

    /// <summary>Checks the URL and the model of the settings.</summary>
    /// <param name="settings">The embeddings settings.</param>
    /// <returns>SND-CODEIDX-005 when the URL is not loopback or the model is a cloud model; otherwise null.</returns>
    public static Error? Check(EmbeddingsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        throw new NotSupportedException();
    }
}
