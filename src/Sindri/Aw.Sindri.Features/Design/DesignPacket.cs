using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>The design packet assembler (line spec §3.2 row 2, §4.4): the thirteen files of the candidate packet, built from the writer's files, the lens log and the scope packet.</summary>
public static class DesignPacket
{
    /// <summary>The packet's files, in the order the packet kind lists them.</summary>
    /// <param name="facts">The design files as the writer left them, the lens log, the run records and the style pins, with the scope packet's files under 'scope/'.</param>
    /// <param name="identity">The packet's id, repository and base commit.</param>
    /// <param name="scopeHash">The frozen scope packet's hash: the design's one parent.</param>
    public static IReadOnlyList<LineFile> Build(LineFacts facts, PacketIdentity identity, string scopeHash) => throw new NotSupportedException();
}
