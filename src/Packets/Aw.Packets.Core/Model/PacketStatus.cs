using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Model;

/// <summary>A packet's status as its manifest records it. The freeze record under packets/ is authoritative.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PacketStatus>))]
public enum PacketStatus
{
    /// <summary>Being designed; not frozen.</summary>
    [JsonStringEnumMemberName("draft")]
    Draft = 1,

    /// <summary>Frozen by hash; builds run against that hash.</summary>
    [JsonStringEnumMemberName("frozen")]
    Frozen = 2,
}
