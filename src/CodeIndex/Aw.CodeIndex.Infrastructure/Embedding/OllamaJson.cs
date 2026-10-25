using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Infrastructure.Embedding;

/// <summary>Ollama's wire formats; only ever used through <see cref="OllamaWire.Options"/> (the AwJson profile).</summary>
[JsonSerializable(typeof(EmbedRequest))]
[JsonSerializable(typeof(EmbedResponse))]
[JsonSerializable(typeof(TagsResponse))]
internal sealed partial class OllamaJson : JsonSerializerContext;
