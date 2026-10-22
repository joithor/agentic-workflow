using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Model;

/// <summary>One kind of data in an index (spec §3): structure, clones and deps are always built; graph and embeddings are optional.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LayerName>))]
public enum LayerName
{
    /// <summary>Files, symbols and call sites.</summary>
    [JsonStringEnumMemberName("structure")]
    Structure,

    /// <summary>Shape hashes and MinHash bands.</summary>
    [JsonStringEnumMemberName("clones")]
    Clones,

    /// <summary>Dependency rows read from manifests.</summary>
    [JsonStringEnumMemberName("deps")]
    Deps,

    /// <summary>graphify's symbols and edges.</summary>
    [JsonStringEnumMemberName("graph")]
    Graph,

    /// <summary>Vectors from the loopback embedding model.</summary>
    [JsonStringEnumMemberName("embeddings")]
    Embeddings,
}
