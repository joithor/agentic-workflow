using System.Text.Json.Serialization;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>A phase packet's holdout.json: the hash and size of the phase's hidden set, never its text (PKT-HOLD-002).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PhaseHoldout
{
    /// <summary>An editor hint ('$schema'); not checked and not part of the hash.</summary>
    [JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>The file's schema version; this build reads version 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The 'sha256:' hash of the set in the holdout store.</summary>
    public required PacketHash SetHash { get; init; }

    /// <summary>How many scenarios the set holds.</summary>
    public required int Count { get; init; }
}
