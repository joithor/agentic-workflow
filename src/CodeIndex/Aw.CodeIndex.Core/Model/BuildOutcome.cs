using System.Text.Json.Serialization;

namespace Aw.CodeIndex.Core.Model;

/// <summary>How a build ended (spec §5.7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BuildOutcome>))]
public enum BuildOutcome
{
    /// <summary>An index at the commit was published.</summary>
    [JsonStringEnumMemberName("published")]
    Published,

    /// <summary>The commit left the default branch since the tick; nothing changed.</summary>
    [JsonStringEnumMemberName("not-on-main")]
    NotOnMain,
}
