using Aw.Packets.Core.Ids;

namespace Aw.Packets.Core.Model;

/// <summary>The packet manifest, packet.json (addendum §17.4): id, repo, base sha, status, hash and links.</summary>
public sealed record PacketManifest
{
    /// <summary>The manifest schema version; this build reads version 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The packet id.</summary>
    public required PacketId Id { get; init; }

    /// <summary>The repository the packet builds, for example 'owner/name'.</summary>
    public required string Repo { get; init; }

    /// <summary>The full commit sha write scopes are checked against.</summary>
    public required string BaseSha { get; init; }

    /// <summary>The status the manifest records (not part of the hash).</summary>
    public required PacketStatus Status { get; init; }

    /// <summary>The frozen hash the manifest records, if any (not part of the hash).</summary>
    public string? Hash { get; init; }

    /// <summary>The linked files.</summary>
    public required PacketLinks Links { get; init; }
}
