using Aw.Packets.Core.Values;

namespace Aw.Packets.Tests.Ids;

/// <summary>A nullable PacketHash, as packet.json's 'hash' is: null round-trips as null.</summary>
public sealed record OptionalPacketHashHolder(PacketHash? Id);
