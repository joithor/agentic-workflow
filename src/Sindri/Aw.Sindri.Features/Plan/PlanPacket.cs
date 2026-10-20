using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Plan;

/// <summary>The plan packet assembler (line spec §3.2 row 3): the seven files of the candidate packet, graph.json derived by code.</summary>
public static class PlanPacket
{
    /// <summary>The packet's files.</summary>
    /// <param name="facts">The plan files (tasks.json, exec-review.json, holdout.json, answers.jsonl) with the design packet's under 'design/'.</param>
    /// <param name="identity">The packet's id, repository and base commit.</param>
    /// <param name="parents">The frozen scope packet's hash and the design packet's candidate hash.</param>
    /// <param name="options">The profile values graph.json is derived with.</param>
    public static IReadOnlyList<LineFile> Build(LineFacts facts, PacketIdentity identity, PlanParents parents, PlanGraphOptions options) => throw new NotSupportedException();
}
