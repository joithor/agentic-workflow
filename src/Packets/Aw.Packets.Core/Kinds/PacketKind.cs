namespace Aw.Packets.Core.Kinds;

/// <summary>How a packet file's bytes are read.</summary>
public enum PacketFileFormat
{
    /// <summary>One JSON object (strict: no byte order mark, no duplicate keys, no null in a list).</summary>
    Json = 1,

    /// <summary>One JSON object per non-blank line.</summary>
    JsonLines = 2,

    /// <summary>UTF-8 text.</summary>
    Text = 3,
}

/// <summary>One file a packet kind lists.</summary>
/// <param name="Name">The file name inside the packet directory (no sub-directories).</param>
/// <param name="Format">How it is read.</param>
public sealed record PacketFileSpec(string Name, PacketFileFormat Format);

/// <summary>
/// A kind of packet: a name and the fixed files its schema lists. The <c>build</c> kind (addendum §17.4) has no fixed
/// list: <c>packet.json</c> links its files. Every other kind lists them here, so a file name is never a path a caller chose.
/// </summary>
/// <param name="Name">The kind's name, as <c>--kind</c> takes it.</param>
/// <param name="Files">The files the kind lists; empty for the manifest-driven build kind.</param>
public sealed record PacketKind(string Name, IReadOnlyList<PacketFileSpec> Files)
{
    /// <summary>True when <c>packet.json</c> links the files (the build kind); false when the kind lists them.</summary>
    public bool IsManifestDriven => Files.Count == 0;
}
