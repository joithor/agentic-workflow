using System.Text.Json.Serialization;

namespace Aw.Context.Core.Sources;

/// <summary>Whether a source answers from the live system or from a snapshot Sindri keeps.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceFreshness>))]
public enum SourceFreshness
{
    /// <summary>Asked live on every query.</summary>
    [JsonStringEnumMemberName("live")]
    Live,

    /// <summary>Answered from an index Sindri refreshes (repo-docs' FTS5 chunks).</summary>
    [JsonStringEnumMemberName("snapshot")]
    Snapshot,
}
