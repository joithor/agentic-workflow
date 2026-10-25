using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Model;

/// <summary>Why the snapshot left a tree entry out (spec §5.1); each is counted, never listed with its text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SkipReason>))]
public enum SkipReason
{
    /// <summary>A symlink (mode 120000).</summary>
    [JsonStringEnumMemberName("symlink")]
    Symlink,

    /// <summary>A submodule entry (mode 160000).</summary>
    [JsonStringEnumMemberName("submodule")]
    Submodule,

    /// <summary>Matched a deny glob.</summary>
    [JsonStringEnumMemberName("denied")]
    Denied,

    /// <summary>Over the per-file size cap.</summary>
    [JsonStringEnumMemberName("too-large")]
    TooLarge,

    /// <summary>An empty, '..', '.git', backslash, NUL or absolute component.</summary>
    [JsonStringEnumMemberName("unsafe-path")]
    UnsafePath,

    /// <summary>The blob is not in the object store (a promisor clone); never fetched.</summary>
    [JsonStringEnumMemberName("missing")]
    Missing,
}
