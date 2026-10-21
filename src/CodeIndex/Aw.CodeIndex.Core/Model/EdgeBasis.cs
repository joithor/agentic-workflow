using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Model;

/// <summary>Where an edge came from (spec §3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EdgeBasis>))]
public enum EdgeBasis
{
    /// <summary>The parser's call sites, resolved by name.</summary>
    [JsonStringEnumMemberName("syntax")]
    Syntax,

    /// <summary>graphify's graph, merged in.</summary>
    [JsonStringEnumMemberName("graphify")]
    Graphify,
}
