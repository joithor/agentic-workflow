using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Features.FreezeScopingPacket;

/// <summary>The FreezeScopingPacket response, also the '--json' output (schemaVersion 1).</summary>
internal sealed record FreezeScopingPacketResponse
{
    /// <summary>The output schema version.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The packet kind ('scoping').</summary>
    public required string Kind { get; init; }

    /// <summary>The scoping instance the freeze is recorded under.</summary>
    public required PacketId Instance { get; init; }

    /// <summary>Who froze it, as this call asked (an already frozen hash keeps the 'by' it was first recorded with).</summary>
    public FreezeBy By { get; init; } = FreezeBy.Machine;

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
