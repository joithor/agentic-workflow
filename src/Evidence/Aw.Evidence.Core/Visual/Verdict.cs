using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Visual;

/// <summary>A run's visual verdict.</summary>
[JsonConverter(typeof(StrictEnumConverter<Verdict>))]
public enum Verdict
{
    /// <summary>No verdict: nothing passed to judge, or the judge was unavailable (fail closed).</summary>
    [JsonStringEnumMemberName("unchecked")]
    Unchecked,

    /// <summary>Pixel-identical to the approved baseline; no model consulted.</summary>
    [JsonStringEnumMemberName("unchanged")]
    Unchanged,

    /// <summary>The judge says it looks right.</summary>
    [JsonStringEnumMemberName("looks-right")]
    LooksRight,

    /// <summary>The judge says something looks wrong.</summary>
    [JsonStringEnumMemberName("looks-off")]
    LooksOff,

    /// <summary>The judge says it looks careless.</summary>
    [JsonStringEnumMemberName("sloppy")]
    Sloppy,
}
