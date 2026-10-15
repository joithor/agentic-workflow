using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Proposals;

/// <summary>One scoped surface and where it stands.</summary>
/// <param name="Surface">The name the run gave it.</param>
/// <param name="Node">The node it resolved to, or null when unmapped.</param>
/// <param name="Status">Mapped, new or unmapped.</param>
public sealed record GraphStatus(string Surface, NodeKey? Node, GraphStatusKind Status);
