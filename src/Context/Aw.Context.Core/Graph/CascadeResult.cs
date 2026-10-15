namespace Aw.Context.Core.Graph;

/// <summary>What a change to an anchor touches (spec §6.1).</summary>
/// <param name="OwnScope">The anchor and its own scope, free of cost: descendant features, their surfaces and those surfaces' actions (the anchor first, then by key).</param>
/// <param name="Affected">The nodes the steps reached, by depth, then key; none of them is in <paramref name="OwnScope"/>.</param>
public sealed record CascadeResult(IReadOnlyList<NodeKey> OwnScope, IReadOnlyList<AffectedNode> Affected);
