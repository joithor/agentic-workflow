using System.Text.Json.Serialization;

namespace Aw.Context.Core.Review;

/// <summary>
/// What one review call does. Sindri's catalog classes them (X63): accept, reject, rename, move, accept-deterministic,
/// undo, drop and repoint are 'desk' (the app or a terminal); apply-file, merge, add and delete are 'protected' (a terminal, typed confirmation).
/// Every verb but skip (which writes nothing and isn't a call) is one override row, accept-deterministic one row per entry.
/// </summary>
[JsonConverter(typeof(StrictEnumConverter<ReviewVerb>))]
public enum ReviewVerb
{
    /// <summary>Accepts a node or a link.</summary>
    [JsonStringEnumMemberName("accept")]
    Accept,

    /// <summary>Rejects a node or a link.</summary>
    [JsonStringEnumMemberName("reject")]
    Reject,

    /// <summary>Gives a node a new label.</summary>
    [JsonStringEnumMemberName("rename")]
    Rename,

    /// <summary>Moves a node under another feature.</summary>
    [JsonStringEnumMemberName("move")]
    Move,

    /// <summary>Accepts every deterministic proposal in the queue.</summary>
    [JsonStringEnumMemberName("accept-deterministic")]
    AcceptDeterministic,

    /// <summary>Reverses one earlier override.</summary>
    [JsonStringEnumMemberName("undo")]
    Undo,

    /// <summary>Drops an orphaned override.</summary>
    [JsonStringEnumMemberName("drop")]
    Drop,

    /// <summary>Folds a node into another.</summary>
    [JsonStringEnumMemberName("merge")]
    Merge,

    /// <summary>Adds a node the build doesn't find.</summary>
    [JsonStringEnumMemberName("add")]
    Add,

    /// <summary>Removes a node.</summary>
    [JsonStringEnumMemberName("delete")]
    Delete,

    /// <summary>Points an orphaned override at a node of the draft (an undo of the orphan and the same decision on the new node).</summary>
    [JsonStringEnumMemberName("repoint")]
    Repoint,
}
