using System.Text.Json.Serialization;

namespace Aw.Context.Core.Results;

/// <summary>How a path changed between the merge base and the head of a diff range (a rename is a deletion plus an addition).</summary>
[JsonConverter(typeof(StrictEnumConverter<ChangeKind>))]
public enum ChangeKind
{
    /// <summary>The path is new.</summary>
    [JsonStringEnumMemberName("added")]
    Added,

    /// <summary>The path's content or mode changed (any change git reports that is not an addition, deletion or type change).</summary>
    [JsonStringEnumMemberName("modified")]
    Modified,

    /// <summary>The path is gone.</summary>
    [JsonStringEnumMemberName("deleted")]
    Deleted,

    /// <summary>The path changed type (a file became a symlink or the other way round).</summary>
    [JsonStringEnumMemberName("typeChanged")]
    TypeChanged,
}
