namespace Aw.Packets.Core.Kinds;

/// <summary>
/// A kind of packet: a name and the fixed files its schema lists. The 'build' kind (addendum §17.4) has no fixed
/// list: 'packet.json' links its files. Every other kind lists them here, so a file name is never a path a caller chose.
/// A line kind (design, plan, phase, proof, review, handoff: line spec §3.2) lists 'packet.json' and 'index.json' among them.
/// </summary>
/// <param name="Name">The kind's name, as '--kind' takes it.</param>
/// <param name="Files">The files the kind lists; empty for the manifest-driven build kind.</param>
public sealed record PacketKind(string Name, IReadOnlyList<PacketFileSpec> Files)
{
    /// <summary>The manifest's file name (the build kind links its files from it; a line kind lists it among its own).</summary>
    public const string ManifestFile = "packet.json";

    /// <summary>The sub-directories the kind allows beside its files (a proof pack's 'app-run/' and 'evidence/').</summary>
    public IReadOnlyList<PacketLinkedDirectory> Directories { get; init; } = [];

    /// <summary>True when 'packet.json' links the files (the build kind); false when the kind lists them.</summary>
    public bool IsManifestDriven => Files.Count == 0;

    /// <summary>True for a line kind: it lists its files, and its 'packet.json' links each of them.</summary>
    public bool IsLine => Files.Any(static f => string.Equals(f.Name, ManifestFile, StringComparison.Ordinal));
}
