using System.Text.Json.Serialization;

namespace Aw.Context.Core.Lenses;

/// <summary>Which lens a scope node lacks.</summary>
[JsonConverter(typeof(StrictEnumConverter<GapKind>))]
public enum GapKind
{
    /// <summary>No record mentions it and no decision decides it.</summary>
    [JsonStringEnumMemberName("intent")]
    Intent,

    /// <summary>No file confirmed-implements it.</summary>
    [JsonStringEnumMemberName("code")]
    Code,
}
