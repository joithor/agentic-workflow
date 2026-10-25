using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Model;

/// <summary>A layer's status in an index (the 'layers.status' column, spec §4.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LayerState>))]
public enum LayerState
{
    /// <summary>Built and current.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,

    /// <summary>Could not be built; the layer's code says why.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,

    /// <summary>Turned off by the profile.</summary>
    [JsonStringEnumMemberName("disabled")]
    Disabled,

    /// <summary>Partly built, more to come (embeddings over several builds).</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,
}
