using System.Text.Json.Serialization;

namespace Aw.Context.Core.Map;

/// <summary>A map doctor check's outcome; Sindri maps it to its own check state.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MapHealthState>))]
public enum MapHealthState
{
    /// <summary>Nothing to do.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,

    /// <summary>Works, with something to look at.</summary>
    [JsonStringEnumMemberName("warn")]
    Warn,

    /// <summary>Broken.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail,
}
