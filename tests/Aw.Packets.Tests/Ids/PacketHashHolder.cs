using Aw.Packets.Core.Values;

namespace Aw.Packets.Tests.Ids;

/// <summary>Wraps a PacketHash so the JSON tests can send a missing or null property.</summary>
public sealed record PacketHashHolder(PacketHash Id);
