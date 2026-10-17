using System.Text.Json.Serialization;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>A phase's base rule and what it points at: the branch name for 'default-branch', the dependency's phase id for 'dependency-head'.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PhaseBase
{
    /// <summary>The rule.</summary>
    public required BaseRule Rule { get; init; }

    /// <summary>The branch or phase the rule points at.</summary>
    public required string Ref { get; init; }
}
