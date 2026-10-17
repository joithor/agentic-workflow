using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>A line kind's packet.json with its kind-specific links flattened, so the checks need no generic code.</summary>
/// <param name="SchemaVersion">The manifest schema version.</param>
/// <param name="Kind">The kind the manifest says it is.</param>
/// <param name="Id">The packet id.</param>
/// <param name="Repo">The repository the packet is about.</param>
/// <param name="BaseSha">The base commit, as text so PKT-BASE-001 reports a short or malformed one.</param>
/// <param name="Status">The status the manifest records.</param>
/// <param name="Hash">The frozen hash the manifest records, if any.</param>
/// <param name="Links">Each link key with the paths it names.</param>
public sealed record LineManifest(
    int SchemaVersion,
    string Kind,
    PacketId Id,
    RepoSlug Repo,
    string BaseSha,
    PacketStatus Status,
    PacketHash? Hash,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Links);
