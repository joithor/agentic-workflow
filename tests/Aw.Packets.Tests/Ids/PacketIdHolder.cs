using Aw.Packets.Core.Ids;

namespace Aw.Packets.Tests.Ids;

/// <summary>Wraps a PacketId so the JSON tests can send a missing or null property.</summary>
public sealed record PacketIdHolder(PacketId Id);
