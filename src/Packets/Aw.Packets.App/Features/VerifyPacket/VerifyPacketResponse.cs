using System.Text.Json.Serialization;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.VerifyPacket;

/// <summary>The VerifyPacket response, also the '--json' output (schemaVersion 1).</summary>
internal sealed record VerifyPacketResponse
{
    /// <summary>The output schema version.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The packet kind.</summary>
    public required string Kind { get; init; }

    /// <summary>The packet id; null when it isn't known (a candidate scope packet, or a packet.json that can't be read).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PacketId? Packet { get; init; }

    /// <summary>True when the packet hashes to the expected hash and (unless a candidate) that hash is frozen.</summary>
    public required bool Verified { get; init; }

    /// <summary>True when only the hash was compared.</summary>
    public required bool Candidate { get; init; }

    /// <summary>The hash that was expected.</summary>
    public required PacketHash Expected { get; init; }

    /// <summary>The hash the packet has now.</summary>
    public required PacketHash Computed { get; init; }

    /// <summary>The canonical form's version.</summary>
    public required int DigestVersion { get; init; }

    /// <summary>Whether a freeze record was found; null for a candidate (none was looked for).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Frozen { get; init; }

    /// <summary>Who froze it, from the record (a record without 'by' says machine); null when no record was found.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FreezeBy? By { get; init; }

    /// <summary>When it was frozen, from the record; null when no record was found.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? FrozenAt { get; init; }

    /// <summary>Where the record is (or would be), relative to the packets/ namespace; null for a candidate or when the packet can't be located.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Location { get; init; }

    /// <summary>Why it isn't verified; null when it is.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}
