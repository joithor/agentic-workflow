using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>An existing test file the phase may change, and why (line spec §6.2 'testChange'; shown at stop 2).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TestChange
{
    /// <summary>The test file, relative to the repository root.</summary>
    public required string Path { get; init; }

    /// <summary>Why the change is needed.</summary>
    public required string Reason { get; init; }
}
