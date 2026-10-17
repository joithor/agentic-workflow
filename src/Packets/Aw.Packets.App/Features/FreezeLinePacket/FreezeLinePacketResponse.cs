using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.FreezeLinePacket;

/// <summary>The FreezeLinePacket response, also the '--json' output (schemaVersion 1).</summary>
internal sealed record FreezeLinePacketResponse
{
    /// <summary>The output schema version.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The packet kind.</summary>
    public required string Kind { get; init; }

    /// <summary>The packet id; null when packet.json can't be read.</summary>
    public PacketId? Packet { get; init; }

    /// <summary>Who froze it, as the stored record says: this call's 'by' when it recorded the freeze, the first freeze's when the hash was frozen before (a freeze is write-once).</summary>
    public FreezeBy By { get; init; } = FreezeBy.Machine;

    /// <summary>When the stored record was written; null when nothing was frozen.</summary>
    public DateTimeOffset? FrozenAt { get; init; }

    /// <summary>Set when the hash was already frozen by someone other than this call asked: who froze it first. The call's 'by' was not recorded; null otherwise.</summary>
    public FreezeBy? AlreadyFrozenBy { get; init; }

    /// <summary>True when the packet passed its checks and the freeze is recorded (now or before).</summary>
    public required bool Frozen { get; init; }

    /// <summary>The canonical hash; null when the checks failed.</summary>
    public PacketHash? Hash { get; init; }

    /// <summary>The canonical form's version.</summary>
    public required int DigestVersion { get; init; }

    /// <summary>The record's path relative to the packets/ namespace; null when nothing was frozen.</summary>
    public string? Location { get; init; }

    /// <summary>True when the same hash was frozen before (nothing was written).</summary>
    public required bool AlreadyFrozen { get; init; }

    /// <summary>Every check outcome, as validate reports them.</summary>
    public required IReadOnlyList<CheckResult> Checks { get; init; }
}
