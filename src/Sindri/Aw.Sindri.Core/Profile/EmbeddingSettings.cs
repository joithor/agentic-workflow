using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The 'index.embeddings' keys (code index spec §13): loopback Ollama only.</summary>
/// <param name="Enabled">Off leaves the embeddings layer disabled (default true).</param>
/// <param name="Url">The embedding server, a loopback IP (default <see cref="DefaultUrl"/>).</param>
/// <param name="Model">The embedding model (default the code index lock's); a cloud model is refused.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmbeddingSettings(bool? Enabled = null, string? Url = null, string? Model = null)
{
    /// <summary>The default for 'url'.</summary>
    public const string DefaultUrl = "http://127.0.0.1:11434";
}
