using System.Text.Json.Serialization;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>One piece of evidence for a passing item (line spec LC-2): what it is, where it is, its hash, the commit it ran on and when.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EvidenceRef
{
    /// <summary>The evidence kind, for example 'test' or 'api-transcript'.</summary>
    public required string Kind { get; init; }

    /// <summary>Where the artifact is, relative to the packet or the run area.</summary>
    public required string Path { get; init; }

    /// <summary>The artifact's 'sha256:' hash.</summary>
    public required PacketHash Sha256 { get; init; }

    /// <summary>The commit the evidence ran on (LC-4).</summary>
    public required CommitSha Commit { get; init; }

    /// <summary>When it was produced (UTC).</summary>
    public required DateTimeOffset At { get; init; }
}
