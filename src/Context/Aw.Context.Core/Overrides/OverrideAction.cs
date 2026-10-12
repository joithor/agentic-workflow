using System.Text.Json.Serialization;

namespace Aw.Context.Core.Overrides;

/// <summary>What an override does to the map (spec §6.1), applied over every rebuild and import.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OverrideAction>))]
public enum OverrideAction
{
    /// <summary>A human accepts a node: it counts as human-written (trusted).</summary>
    [JsonStringEnumMemberName("accept")]
    Accept,

    /// <summary>A human rejects a proposed node; it stays rejected while its rejection identity holds.</summary>
    [JsonStringEnumMemberName("reject")]
    Reject,

    /// <summary>A new label.</summary>
    [JsonStringEnumMemberName("rename")]
    Rename,

    /// <summary>Folds the target into another node: its edges move there and its key becomes an alias.</summary>
    [JsonStringEnumMemberName("merge")]
    Merge,

    /// <summary>Puts the target under another feature (its part-of edge).</summary>
    [JsonStringEnumMemberName("move")]
    Move,

    /// <summary>Adds a node the build doesn't find.</summary>
    [JsonStringEnumMemberName("add")]
    Add,

    /// <summary>Removes a node and its edges.</summary>
    [JsonStringEnumMemberName("delete")]
    Delete,
}
