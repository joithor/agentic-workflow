using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>What a phase does (line spec §6.2 'kind'); a 'fix' phase's items need base+head signals.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PhaseKind>))]
public enum PhaseKind
{
    /// <summary>New behaviour.</summary>
    [JsonStringEnumMemberName("feature")]
    Feature = 1,

    /// <summary>A bug fix.</summary>
    [JsonStringEnumMemberName("fix")]
    Fix = 2,

    /// <summary>A change that keeps behaviour.</summary>
    [JsonStringEnumMemberName("refactor")]
    Refactor = 3,

    /// <summary>Documentation only.</summary>
    [JsonStringEnumMemberName("docs")]
    Docs = 4,

    /// <summary>Configuration only.</summary>
    [JsonStringEnumMemberName("config")]
    Config = 5,
}
