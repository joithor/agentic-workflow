using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Review;

/// <summary>Small draft graphs the review, proposal and override tests share.</summary>
internal static class SampleDrafts
{
    public static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-01T09:00:00Z", CultureInfo.InvariantCulture);

    /// <summary>
    /// A draft as a build leaves it: a confirmed 'tabs' feature, two deterministic commands under it, two model features
    /// (tab groups with two model file links, layout with one) and one deterministic file link.
    /// </summary>
    public static GraphSnapshot Draft() => new GraphBuilder()
        .Node("feature:tabs", "Tabs")
        .Node("surface:command/switch-tab", "Switch tab", Provenance.Deterministic)
        .Node("surface:command/close-tab", "Close tab", Provenance.Deterministic)
        .Node("feature:tabs.groups", "Tab groups", Provenance.Model, 0.7)
        .Node("feature:layout", "Layout", Provenance.Model, 0.6)
        .Edge("surface:command/switch-tab", EdgeType.PartOf, "feature:tabs", provenance: Provenance.Deterministic)
        .Edge("surface:command/close-tab", EdgeType.PartOf, "feature:tabs", provenance: Provenance.Deterministic)
        .Edge("feature:tabs.groups", EdgeType.PartOf, "feature:tabs", provenance: Provenance.Model, confidence: 0.7)
        .Edge("code:src/tabs/Switch.ts", EdgeType.Implements, "surface:command/switch-tab", provenance: Provenance.Deterministic)
        .Edge("code:src/tabs/Groups.ts", EdgeType.Implements, "feature:tabs.groups", provenance: Provenance.Model, confidence: 0.7)
        .Edge("code:src/tabs/GroupList.ts", EdgeType.Implements, "feature:tabs.groups", provenance: Provenance.Model, confidence: 0.6)
        .Edge("code:src/layout/Panes.ts", EdgeType.Implements, "feature:layout", provenance: Provenance.Model, confidence: 0.55)
        .Build();

    public static NodeKey Key(string value) => NodeKey.From(value);

    public static LinkId Link(string from, EdgeType type, string to) => LinkIds.For(new GraphEdge(Key(from), Key(to), type));

    public static MapOverride Row(OverrideAction action, string target, string? label = null, string? other = null, DateTimeOffset? at = null, string? rejection = null, LinkId? link = null) =>
        new(action, Key(target), "avery", at ?? At, label, other is null ? null : Key(other), rejection, link);

    public static MapOverride Undo(MapOverride undone, DateTimeOffset at) =>
        new(OverrideAction.Undo, undone.Target, "avery", at, Undoes: undone.ToId());
}
