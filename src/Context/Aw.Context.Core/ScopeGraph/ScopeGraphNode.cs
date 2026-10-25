namespace Aw.Context.Core.ScopeGraph;

/// <summary>One node of the scoping graph.</summary>
/// <param name="Id">The map key (or the record id, or <c>code:path#symbol</c> for a symbol): stable across calls on one map version.</param>
/// <param name="Kind">The node kind.</param>
/// <param name="Path">The repo path for a code node, else null.</param>
/// <param name="Symbol">The symbol for a symbol node, else null.</param>
/// <param name="Summary">One line saying what the node is.</param>
/// <param name="Rank">1 for the most relevant.</param>
/// <param name="Lens">The lens it was found by: product, related, code or history.</param>
/// <param name="LensItem">The lens item id ('L-code-3') the gather numbered it.</param>
/// <param name="Band">The band, or null.</param>
/// <param name="Citations">Where it is in the repo ('path:line').</param>
public sealed record ScopeGraphNode(string Id, string Kind, string? Path, string? Symbol, string Summary, int Rank, string Lens, string LensItem, string? Band, IReadOnlyList<string> Citations);
