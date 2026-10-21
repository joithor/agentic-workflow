using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>How much of a symbol's calls an edge row can show (code-index spec §5.5): a graphify edge keeps one call site per caller and callee pair, so its answer may miss callers.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CallCoverage>))]
public enum CallCoverage
{
    /// <summary>The parser saw every call site (C#).</summary>
    [JsonStringEnumMemberName("full")]
    Full,

    /// <summary>The edge came from graphify: other call sites may exist, and an empty answer reads 'none found', never 'none'.</summary>
    [JsonStringEnumMemberName("partial")]
    Partial,
}
