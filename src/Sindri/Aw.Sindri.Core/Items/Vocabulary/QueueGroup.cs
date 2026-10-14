using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Items.Vocabulary;

/// <summary>A queue group (API spec §3.6); written in camel case ('waitingOnYou'). <see cref="QueueGroups.Order"/> holds the groups the queue shows.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QueueGroup>))]
public enum QueueGroup
{
    /// <summary>Items that wait on a human: a decision, an answer, a parked instance.</summary>
    [JsonStringEnumMemberName("waitingOnYou")]
    WaitingOnYou,

    /// <summary>Items a human took over.</summary>
    [JsonStringEnumMemberName("withYou")]
    WithYou,

    /// <summary>Parked items, when the queue shows them apart (not in <see cref="QueueGroups.Order"/>: the owner chose four groups).</summary>
    [JsonStringEnumMemberName("parked")]
    Parked,

    /// <summary>Items an instance is running.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>New and queued items.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,
}
