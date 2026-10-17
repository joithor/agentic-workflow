namespace Aw.Packets.Core.Kinds;

/// <summary>
/// A kind of packet: a name and the fixed files its schema lists. The 'build' kind (addendum §17.4) has no fixed
/// list: 'packet.json' links its files. Every other kind lists them here, so a file name is never a path a caller chose.
/// </summary>
/// <param name="Name">The kind's name, as '--kind' takes it.</param>
/// <param name="Files">The files the kind lists; empty for the manifest-driven build kind.</param>
public sealed record PacketKind(string Name, IReadOnlyList<PacketFileSpec> Files)
{
    /// <summary>True when 'packet.json' links the files (the build kind); false when the kind lists them.</summary>
    public bool IsManifestDriven => Files.Count == 0;
}
