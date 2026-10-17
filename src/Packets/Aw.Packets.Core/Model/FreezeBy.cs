using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Model;

/// <summary>Who froze a packet (line spec §2, LN-PKT5): the owner at the desk, or the machine.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FreezeBy>))]
public enum FreezeBy
{
    /// <summary>The machine froze it: every freeze unless the owner asked (and what an old record without 'by' reads as).</summary>
    [JsonStringEnumMemberName("machine")]
    Machine = 1,

    /// <summary>The owner froze it: step.build.amend and the stop blocks.</summary>
    [JsonStringEnumMemberName("owner")]
    Owner = 2,
}
