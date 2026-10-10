using System.Text.Json.Serialization;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Model;

/// <summary>The packet manifest, packet.json (addendum §17.4): id, repo, base sha, status, hash and links.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PacketManifest
{
    /// <summary>An editor hint ('$schema'), for example the path of schemas/packets/packet.schema.json; not checked.</summary>
    [JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>The manifest schema version; this build reads version 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The packet id.</summary>
    public required PacketId Id { get; init; }

    /// <summary>The repository the packet builds, 'owner/name'.</summary>
    public required RepoSlug Repo { get; init; }

    /// <summary>
    /// The full commit sha write scopes are checked against. Text on purpose, not a <see cref="CommitSha"/>: a short or
    /// malformed sha is a PKT-BASE-001 failure that names the fix, not a malformed packet.
    /// </summary>
    public required string BaseSha { get; init; }

    /// <summary>The status the manifest records (not part of the hash).</summary>
    public required PacketStatus Status { get; init; }

    /// <summary>The frozen hash the manifest records, if any (not part of the hash).</summary>
    public PacketHash? Hash { get; init; }

    /// <summary>The linked files.</summary>
    public required PacketLinks Links { get; init; }
}
