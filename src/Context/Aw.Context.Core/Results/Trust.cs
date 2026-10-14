using System.Text.Json.Serialization;

namespace Aw.Context.Core.Results;

/// <summary>Whether text may be read as Sindri's own voice (spec §7, D15): trust follows provenance, not node type.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Trust>))]
public enum Trust
{
    /// <summary>Deterministic extraction, human overrides, accepted model output.</summary>
    [JsonStringEnumMemberName("trusted")]
    Trusted,

    /// <summary>Record text, titles, authors, code text, and anything a model wrote from them until a human accepts it.</summary>
    [JsonStringEnumMemberName("untrusted")]
    Untrusted,
}
