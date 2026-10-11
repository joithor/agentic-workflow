using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Guard;

/// <summary>What the local database check found.</summary>
[JsonConverter(typeof(StrictEnumConverter<DbProvenance>))]
public enum DbProvenance
{
    /// <summary>Only seed rows.</summary>
    [JsonStringEnumMemberName("seeded")]
    Seeded,

    /// <summary>Not proven to be seed data.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}
