namespace Aw.Packets.Core.Kinds;

/// <summary>One file a packet kind lists.</summary>
/// <param name="Name">The file name inside the packet directory (no sub-directories).</param>
/// <param name="Format">How it is read.</param>
public sealed record PacketFileSpec(string Name, PacketFileFormat Format);
