using System.Text.Json.Serialization;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>What an amended phase packet adds (line spec §3.2 row 4): the packet it amends, the paths granted, why, and when.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PhaseAmends
{
    /// <summary>The hash of the phase packet this one amends.</summary>
    public required PacketHash Parent { get; init; }

    /// <summary>The paths added to the allowed files.</summary>
    public required IReadOnlyList<string> Paths { get; init; }

    /// <summary>Why the owner allowed them.</summary>
    public required string Reason { get; init; }

    /// <summary>When (UTC).</summary>
    public required DateTimeOffset At { get; init; }
}
