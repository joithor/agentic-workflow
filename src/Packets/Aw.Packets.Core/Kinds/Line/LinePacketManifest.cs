using System.Text.Json.Serialization;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>
/// packet.json of a line kind (line spec §3.2, LN-PKT1): the build manifest's identity fields, the kind it is, and a
/// 'links' object that names every file the kind lists, so the canonical hash covers them all.
/// </summary>
/// <typeparam name="TLinks">The kind's links.</typeparam>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinePacketManifest<TLinks>
    where TLinks : ILineLinks
{
    /// <summary>An editor hint ('$schema'); not checked and not part of the hash.</summary>
    [JsonPropertyName("$schema"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    /// <summary>The manifest schema version; this build reads version 1.</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The kind: one of design, plan, phase, proof, review, handoff.</summary>
    public required string Kind { get; init; }

    /// <summary>The packet id (a phase packet's is '&lt;line&gt;-&lt;phase&gt;[-a&lt;n&gt;]').</summary>
    public required PacketId Id { get; init; }

    /// <summary>The repository the packet is about, 'owner/name'.</summary>
    public required RepoSlug Repo { get; init; }

    /// <summary>The full commit sha the packet is checked against (PKT-BASE-001); text, so a short sha is a rule failure.</summary>
    public required string BaseSha { get; init; }

    /// <summary>The status the manifest records (not part of the hash).</summary>
    public required PacketStatus Status { get; init; }

    /// <summary>The frozen hash the manifest records, if any (not part of the hash).</summary>
    public PacketHash? Hash { get; init; }

    /// <summary>The linked files.</summary>
    public required TLinks Links { get; init; }

    /// <summary>The checks' view of this manifest.</summary>
    /// <returns>The manifest with its links flattened.</returns>
    public LineManifest ToView() => new(SchemaVersion, Kind, Id, Repo, BaseSha, Status, Hash, Links.Entries());
}
