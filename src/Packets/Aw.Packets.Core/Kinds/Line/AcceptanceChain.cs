using System.Text.Json.Serialization;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>Where a packet sits in the acceptance chain (line spec LC-2): the hash of its own acceptance.json and of the one it extends.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AcceptanceChain
{
    /// <summary>The 'sha256:' hash of this packet's acceptance.json; null for a packet that has none (a plan graph).</summary>
    public required PacketHash? Self { get; init; }

    /// <summary>The 'sha256:' hash of the acceptance.json this one extends (its 'parent'); null for the chain's root.</summary>
    public required PacketHash? Extends { get; init; }
}
