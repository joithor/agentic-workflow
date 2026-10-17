using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>An acceptance item's status (line spec LC-2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AcceptanceStatus>))]
public enum AcceptanceStatus
{
    /// <summary>Not proven yet.</summary>
    [JsonStringEnumMemberName("failing")]
    Failing = 1,

    /// <summary>Proven; only a proof pack flips an item here, with evidence.</summary>
    [JsonStringEnumMemberName("passing")]
    Passing = 2,

    /// <summary>Replaced by a newer item, confirmed at stop 2.</summary>
    [JsonStringEnumMemberName("superseded")]
    Superseded = 3,
}
