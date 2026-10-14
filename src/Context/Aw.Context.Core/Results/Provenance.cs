using System.Text.Json.Serialization;

namespace Aw.Context.Core.Results;

/// <summary>Who produced a fact (spec §6.1).</summary>
[JsonConverter(typeof(StrictEnumConverter<Provenance>))]
public enum Provenance
{
    /// <summary>Deterministic extraction or a source record as it stands.</summary>
    [JsonStringEnumMemberName("deterministic")]
    Deterministic,

    /// <summary>A model wrote it.</summary>
    [JsonStringEnumMemberName("model")]
    Model,

    /// <summary>A human wrote or accepted it.</summary>
    [JsonStringEnumMemberName("human")]
    Human,
}
