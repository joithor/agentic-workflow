using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// The review verbs the app can send: the desk ones only. The review route is one route with a class per verb (the protected
/// apply-file, merge, add and delete share it), so the app holds a closed set that has no protected verb in it, and an
/// architecture test pins every member to a Desk catalog row on that route.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeskVerb>))]
internal enum DeskVerb
{
    /// <summary>Accept a suggested link.</summary>
    [JsonStringEnumMemberName("accept")]
    Accept,

    /// <summary>Reject a suggested link.</summary>
    [JsonStringEnumMemberName("reject")]
    Reject,

    /// <summary>Rename a node.</summary>
    [JsonStringEnumMemberName("rename")]
    Rename,

    /// <summary>Move a node under another.</summary>
    [JsonStringEnumMemberName("move")]
    Move,

    /// <summary>Accept every deterministic suggestion.</summary>
    [JsonStringEnumMemberName("accept-deterministic")]
    AcceptDeterministic,

    /// <summary>Undo one or several overrides.</summary>
    [JsonStringEnumMemberName("undo")]
    Undo,

    /// <summary>Drop an orphaned override.</summary>
    [JsonStringEnumMemberName("drop")]
    Drop,

    /// <summary>Repoint an orphaned override.</summary>
    [JsonStringEnumMemberName("repoint")]
    Repoint,
}
