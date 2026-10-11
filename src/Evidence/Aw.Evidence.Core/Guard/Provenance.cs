using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Guard;

/// <summary>Where the data in a run's screenshots came from, which decides where they may be published.</summary>
[JsonConverter(typeof(StrictEnumConverter<Provenance>))]
public enum Provenance
{
    /// <summary>The local database holds only seed data.</summary>
    [JsonStringEnumMemberName("seeded")]
    Seeded,

    /// <summary>A preview host's scrubbed customer extract.</summary>
    [JsonStringEnumMemberName("scrubbed")]
    Scrubbed,

    /// <summary>Unknown: the evidence stays on this machine.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}
