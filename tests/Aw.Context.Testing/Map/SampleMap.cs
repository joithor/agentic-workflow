using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Testing.Map;

/// <summary>A small checked map for the port contracts: every kind of row, with evidence, timestamps and provenance.</summary>
public static class SampleMap
{
    /// <summary>The time the sample's human rows were written.</summary>
    public static DateTimeOffset At { get; } = DateTimeOffset.Parse("2026-07-15T10:00:00Z", CultureInfo.InvariantCulture);

    /// <summary>The sample, with an optional label suffix so two imports differ.</summary>
    /// <param name="suffix">Appended to the feature's label.</param>
    /// <returns>The map.</returns>
    public static MapImport Build(string suffix = "")
    {
        var feature = NodeKey.From("feature:layout.split-pane");
        var code = NodeKey.From("code:src/layout/SplitPane.ts");
        var record = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
        var decision = new DecisionRow(record, "keeps the focused pane first", "Focus stays first.", Provenance.Human, At);
        using var attributes = JsonDocument.Parse("""{"states":["one","two"]}""");
        GraphNode[] nodes =
        [
            new(feature, "Split pane" + suffix, Provenance.Human, 1.0, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/docs/architecture.md"), "L1-L4")], attributes.RootElement.Clone(), At),
            new(code, "SplitPane.ts", Provenance.Deterministic),
            new(NodeKey.From("feature:layout.guess"), "Guess", Provenance.Model, 0.4),
            new(decision.ToKey(), decision.Wording, Provenance.Human, Evidence: [new EvidenceRef(record)], At: At),
            new(NodeKey.From($"record:{record.Value}"), record.Value, Provenance.Deterministic),
        ];
        GraphEdge[] edges =
        [
            new(code, feature, EdgeType.Implements, Provenance.Deterministic, 1.0, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/src/layout/SplitPane.ts"))]),
            new(NodeKey.From("feature:layout.guess"), feature, EdgeType.PartOf, Provenance.Model, 0.4),
            new(decision.ToKey(), NodeKey.From($"record:{record.Value}"), EdgeType.Quotes, Provenance.Deterministic),
        ];
        var graph = new GraphSnapshot(nodes, edges, [new NodeAlias(NodeKey.From("feature:layout.splits"), feature)], [decision]);
        return new MapImport(ContextRepoName.From("aw-split-panes"), "a41b07d", graph, [new MapOverride(OverrideAction.Reject, NodeKey.From("feature:layout.guess"), "avery", At)]);
    }
}
