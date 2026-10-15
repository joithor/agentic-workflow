using System.Text.Json.Serialization;

namespace Aw.Context.Core.Lenses;

/// <summary>A file or directory's place in the map.</summary>
[JsonConverter(typeof(StrictEnumConverter<FileMapStatus>))]
public enum FileMapStatus
{
    /// <summary>A confirmed 'implements' edge reaches it (a directory: every file under it is mapped).</summary>
    [JsonStringEnumMemberName("mapped")]
    Mapped,

    /// <summary>Only an unconfirmed (model) edge reaches it, or some of a directory's files are only suggested.</summary>
    [JsonStringEnumMemberName("suggested")]
    Suggested,

    /// <summary>Nothing reaches it (a directory: its worst child).</summary>
    [JsonStringEnumMemberName("unmapped")]
    Unmapped,
}
