using System.Text.Json.Serialization;

namespace Aw.Context.Core.Sources;

/// <summary>
/// Where a source runs (README X61, "multiplayer later, not foreclosed"): the contract doesn't assume a local process,
/// so a source a team coordinator shares can sit behind the same port later. Every S-1A source is local.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourcePlacement>))]
public enum SourcePlacement
{
    /// <summary>Runs on this host for this one user (the default; repo-docs).</summary>
    [JsonStringEnumMemberName("local")]
    Local,

    /// <summary>Runs elsewhere and is shared, for example by a team coordinator (X61; none yet).</summary>
    [JsonStringEnumMemberName("shared")]
    Shared,
}
