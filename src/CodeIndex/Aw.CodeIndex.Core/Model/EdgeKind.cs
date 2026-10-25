using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Model;

/// <summary>A directed relation between two symbols (spec §3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EdgeKind>))]
public enum EdgeKind
{
    /// <summary>The source calls the target.</summary>
    [JsonStringEnumMemberName("calls")]
    Calls,

    /// <summary>The source inherits or implements the target.</summary>
    [JsonStringEnumMemberName("inherits")]
    Inherits,
}
