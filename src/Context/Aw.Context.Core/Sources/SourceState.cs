using System.Text.Json.Serialization;

namespace Aw.Context.Core.Sources;

/// <summary>A source's health, as a word.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceState>))]
public enum SourceState
{
    /// <summary>Answering.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,

    /// <summary>Answering, with a problem worth a look (an index never refreshed, a stale cursor).</summary>
    [JsonStringEnumMemberName("degraded")]
    Degraded,

    /// <summary>Not answering; queries go on without it.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,
}
