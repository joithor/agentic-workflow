using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Model;

/// <summary>How sure an edge is (spec §5.4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Confidence>))]
public enum Confidence
{
    /// <summary>One candidate, on this, an unqualified name in the same type, or a matching type name.</summary>
    [JsonStringEnumMemberName("certain")]
    Certain,

    /// <summary>One candidate, otherwise.</summary>
    [JsonStringEnumMemberName("likely")]
    Likely,

    /// <summary>Several candidates; one edge per candidate, at most 8.</summary>
    [JsonStringEnumMemberName("ambiguous")]
    Ambiguous,

    /// <summary>No candidate in this repo (a library call, or generated code); no target.</summary>
    [JsonStringEnumMemberName("unresolved")]
    Unresolved,
}
