namespace Aw.Packets.Core.Kinds;

/// <summary>One file a packet kind lists.</summary>
/// <param name="Name">The file name inside the packet directory (no sub-directories).</param>
/// <param name="Format">How it is read.</param>
/// <param name="Link">The key under 'links' in packet.json that names this file; null for a kind with no packet.json, and for packet.json itself.</param>
public sealed record PacketFileSpec(string Name, PacketFileFormat Format, string? Link = null);
