namespace Aw.Sindri.Ui.Api.Context;

/// <summary>An item's frozen scope: where scoping started and which map it read.</summary>
/// <param name="Anchors">The starting points, once per (key, map version).</param>
/// <param name="MapVersion">The one map version every anchor was read at; null when they were read at different times.</param>
/// <param name="MapVersions">The map version each lens was read at.</param>
/// <param name="MapCommit">The map's commit when one version covers all, else null.</param>
/// <param name="PacketHash">The frozen scoping packet's hash.</param>
/// <param name="FrozenAt">When the scope was approved and frozen.</param>
internal sealed record ScopeView(
    IReadOnlyList<ScopeAnchorView> Anchors,
    string? MapVersion,
    IReadOnlyDictionary<string, string>? MapVersions,
    string? MapCommit,
    string PacketHash,
    DateTimeOffset FrozenAt);
