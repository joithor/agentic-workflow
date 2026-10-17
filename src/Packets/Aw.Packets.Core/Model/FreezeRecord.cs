using System.Text.Json.Serialization;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Model;

/// <summary>One freeze: the packet (for a scope packet, its scoping instance), its canonical hash, and when.</summary>
public sealed record FreezeRecord
{
    /// <summary>The record's schema version (1).</summary>
    public required int SchemaVersion { get; init; }

    /// <summary>The repository the packet builds (records are kept per repository: packet ids are unique only within one).</summary>
    public required RepoSlug Repo { get; init; }

    /// <summary>The packet.</summary>
    public required PacketId Packet { get; init; }

    /// <summary>The canonical hash, 'sha256:&lt;hex&gt;'.</summary>
    public required PacketHash Hash { get; init; }

    /// <summary>The canonical form's version (1 today).</summary>
    public required int DigestVersion { get; init; }

    /// <summary>The packet kind ('build' unless the record says otherwise).</summary>
    public string Kind { get; init; } = PacketKinds.Build.Name;

    /// <summary>The base commit the packet was checked against; null for a kind that has none (a scope packet).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CommitSha? BaseSha { get; init; }

    /// <summary>When it was frozen (UTC).</summary>
    public required DateTimeOffset FrozenAt { get; init; }

    /// <summary>The files the hash covers, relative to the packet directory.</summary>
    public required IReadOnlyList<string> Files { get; init; }
}
