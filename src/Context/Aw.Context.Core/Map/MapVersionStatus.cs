using System.Text.Json.Serialization;

namespace Aw.Context.Core.Map;

/// <summary>A map version's place in its lifecycle (spec §6.2, §8.7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MapVersionStatus>))]
public enum MapVersionStatus
{
    /// <summary>Being written in batches; never read.</summary>
    [JsonStringEnumMemberName("building")]
    Building,

    /// <summary>Built, waiting for review and promotion.</summary>
    [JsonStringEnumMemberName("draft")]
    Draft,

    /// <summary>The one version queries read.</summary>
    [JsonStringEnumMemberName("current")]
    Current,

    /// <summary>An earlier current version, kept as a base for overrides and as-of maps (the last 20).</summary>
    [JsonStringEnumMemberName("retained")]
    Retained,
}
