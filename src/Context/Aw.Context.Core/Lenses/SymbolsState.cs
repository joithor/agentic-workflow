using System.Text.Json.Serialization;

namespace Aw.Context.Core.Lenses;

/// <summary>Whether the code index can answer for symbols.</summary>
[JsonConverter(typeof(StrictEnumConverter<SymbolsState>))]
public enum SymbolsState
{
    /// <summary>No code index exists.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,

    /// <summary>The index exists but is older than the map.</summary>
    [JsonStringEnumMemberName("stale")]
    Stale,

    /// <summary>The index answers.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,
}
