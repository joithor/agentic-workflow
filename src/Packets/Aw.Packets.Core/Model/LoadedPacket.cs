namespace Aw.Packets.Core.Model;

/// <summary>A packet as loaded: the parsed model plus every file's bytes, keyed by path relative to the packet directory.</summary>
/// <param name="Packet">The parsed packet.</param>
/// <param name="Files">packet.json and every linked file, by relative path with '/' separators.</param>
public sealed record LoadedPacket(Packet Packet, IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Files);
