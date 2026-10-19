using System.Text.Json.Serialization;

namespace Aw.Context.Core.History;

/// <summary>What kind of landed change a history entry is.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HistoryChangeKind>))]
public enum HistoryChangeKind
{
    /// <summary>A merge commit.</summary>
    [JsonStringEnumMemberName("merge")]
    Merge,

    /// <summary>Any other commit, a squash included.</summary>
    [JsonStringEnumMemberName("commit")]
    Commit,
}
