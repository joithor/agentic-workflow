using System.Text.Json.Serialization;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>index.json of a line kind (line spec §3.2): the packet's kind, its parents' hashes and the acceptance chain it extends.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PacketIndex
{
    /// <summary>An editor hint ('$schema'); not checked and not part of the hash.</summary>
    [JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>The index schema version; this build reads version 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The packet's kind; equals packet.json's.</summary>
    public required string Kind { get; init; }

    /// <summary>The hashes of the packets this one builds on, each once.</summary>
    public required IReadOnlyList<PacketHash> Parents { get; init; }

    /// <summary>The acceptance chain.</summary>
    public required AcceptanceChain Acceptance { get; init; }
}
