using System.Text.Json.Serialization;

namespace Aw.Context.Core.Eval;

/// <summary>What a right answer to a probe names, and what it must not.</summary>
/// <param name="Nodes">Graph node keys.</param>
/// <param name="AbsentNodes">Node keys the answer must not name (a sibling the cascade must not reach).</param>
/// <param name="Records">Canonical record keys the answer cites.</param>
/// <param name="Files">Repo-relative paths the answer points at.</param>
/// <param name="Decisions">Decisions the answer carries (checked once decisions are extracted, S-5B; read and kept now).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProbeExpect(
    IReadOnlyList<string>? Nodes = null,
    IReadOnlyList<string>? AbsentNodes = null,
    IReadOnlyList<string>? Records = null,
    IReadOnlyList<string>? Files = null,
    IReadOnlyList<ProbeDecision>? Decisions = null);
