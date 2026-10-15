using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Proposals;

/// <summary>One scoped surface and where it stands.</summary>
/// <param name="Surface">The name the run gave it.</param>
/// <param name="Node">The node it resolved to, or null when unmapped.</param>
/// <param name="Status">Mapped, new or unmapped.</param>
/// <param name="Trust">Always untrusted: the surface name is text a scoping run (a model) wrote, so a client fences it when it reads it as a person; the server has already fenced it for a session.</param>
public sealed record GraphStatus(string Surface, NodeKey? Node, GraphStatusKind Status, Results.Trust Trust = Results.Trust.Untrusted);
