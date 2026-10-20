namespace Aw.Sindri.Features.GetItemScope;

/// <summary>What an item's latest frozen scoping packet says about where to start (its lenses.json, never the scope map).</summary>
/// <param name="Anchors">One entry per distinct (key, map version) across the four lenses, ordered by key then version.</param>
/// <param name="MapVersion">The common 'v&lt;N&gt;' when every lens read the same version, else null ("read at different times").</param>
/// <param name="MapVersions">Each lens's 'v&lt;N&gt;'.</param>
/// <param name="MapCommit">The commit when every lens names the same one, else null.</param>
/// <param name="PacketHash">The packet's canonical hash.</param>
/// <param name="FrozenAt">When the packet was approved.</param>
public sealed record ItemScope(IReadOnlyList<ScopeAnchor> Anchors, string? MapVersion, ScopeMapVersions MapVersions, string? MapCommit, string PacketHash, DateTimeOffset FrozenAt);
