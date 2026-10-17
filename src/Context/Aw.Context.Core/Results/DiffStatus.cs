using System.Text.Json.Serialization;

namespace Aw.Context.Core.Results;

/// <summary>A changed file's place in the map; the same words as the file map's status.</summary>
[JsonConverter(typeof(StrictEnumConverter<DiffStatus>))]
public enum DiffStatus
{
    /// <summary>A confirmed 'implements' edge reaches it.</summary>
    [JsonStringEnumMemberName("mapped")]
    Mapped,

    /// <summary>Only an unconfirmed (model) edge reaches it.</summary>
    [JsonStringEnumMemberName("suggested")]
    Suggested,

    /// <summary>Nothing reaches it.</summary>
    [JsonStringEnumMemberName("unmapped")]
    Unmapped,
}
