using System.Text.Json.Serialization;

namespace Aw.Context.Core.Lenses;

/// <summary>What one source gave a lenses answer (scoping spec C6): a separate word from the sources view's, because a source the profile doesn't name is not a failing one.</summary>
[JsonConverter(typeof(StrictEnumConverter<SearchedState>))]
public enum SearchedState
{
    /// <summary>Read, and answered.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,

    /// <summary>The profile doesn't name it.</summary>
    [JsonStringEnumMemberName("not-configured")]
    NotConfigured,

    /// <summary>Named, but nothing could be read from it yet (a recorded limit such as an index not built, or a sync that never ran).</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,

    /// <summary>Named, and answered from an older or partial source (a code index at another commit, a layer down), or its last run failed; <see cref="SearchedSource.Code"/> and <see cref="SearchedSource.Reason"/> say why.</summary>
    [JsonStringEnumMemberName("degraded")]
    Degraded,
}
