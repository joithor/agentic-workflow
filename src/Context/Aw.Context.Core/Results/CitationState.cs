using System.Text.Json.Serialization;

namespace Aw.Context.Core.Results;

/// <summary>Whether a citation still matches its source (spec §8.4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CitationState>))]
public enum CitationState
{
    /// <summary>The cited version is the source's current one.</summary>
    [JsonStringEnumMemberName("current")]
    Current,

    /// <summary>The source has moved on: both versions are listed.</summary>
    [JsonStringEnumMemberName("stale")]
    Stale,

    /// <summary>The version check didn't run in time.</summary>
    [JsonStringEnumMemberName("unchecked")]
    Unchecked,

    /// <summary>The source has no version for it; it carries the time it was fetched.</summary>
    [JsonStringEnumMemberName("unstable")]
    Unstable,

    /// <summary>Its text was withheld by the scrubber; the reference is kept.</summary>
    [JsonStringEnumMemberName("scrub-blocked")]
    ScrubBlocked,
}
