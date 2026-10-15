using System.Text.Json.Serialization;

namespace Aw.Context.Core.Lenses;

/// <summary>How two items disagree.</summary>
[JsonConverter(typeof(StrictEnumConverter<ConflictKind>))]
public enum ConflictKind
{
    /// <summary>A decision replaces an earlier one.</summary>
    [JsonStringEnumMemberName("supersedes")]
    Supersedes,

    /// <summary>A decision disagrees with a cited record (arrives with S-CF).</summary>
    [JsonStringEnumMemberName("contradicts")]
    Contradicts,
}
