using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Model;

/// <summary>How much of a language the index measures, per row and per answer (spec §5.5, §7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Coverage>))]
public enum Coverage
{
    /// <summary>A C# row: the syntax parser measured it.</summary>
    [JsonStringEnumMemberName("full")]
    Full,

    /// <summary>A graphify language: edges are best effort, so an empty answer reads 'none found', never 'none'.</summary>
    [JsonStringEnumMemberName("partial")]
    Partial,

    /// <summary>A language the index does not measure at all.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,
}
