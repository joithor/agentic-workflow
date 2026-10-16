using System.Text.Json.Serialization;

namespace Aw.Context.Core.Lenses;

/// <summary>What a file-map entry is.</summary>
[JsonConverter(typeof(StrictEnumConverter<FileEntryType>))]
public enum FileEntryType
{
    /// <summary>A directory.</summary>
    [JsonStringEnumMemberName("dir")]
    Dir,

    /// <summary>A file.</summary>
    [JsonStringEnumMemberName("file")]
    File,
}
