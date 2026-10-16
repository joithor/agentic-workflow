using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>Why 'recipe list --stale' lists an approved recipe (spec §4.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StaleReason>))]
public enum StaleReason
{
    /// <summary>A pinned block's descriptor hash differs from this build's: re-approve the same hash (a drift approval).</summary>
    [JsonStringEnumMemberName("pin-drift")]
    PinDrift = 1,

    /// <summary>A pinned child has a newer approved version, or the pinned one was revoked: re-approve the parent to pin the latest.</summary>
    [JsonStringEnumMemberName("child-stale")]
    ChildStale = 2,

    /// <summary>The approved recipe was registered before input kinds (RCP-INPUT-001/002): it can't start or be named; register the current file at a new version and approve it.</summary>
    [JsonStringEnumMemberName("legacy-shape")]
    LegacyShape = 3,
}
