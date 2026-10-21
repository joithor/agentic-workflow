using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Code;

/// <summary>A small sharing graph for the code lens: a feature with a surface, two actions, data things and code files.</summary>
internal static class CodeGraphs
{
    internal static readonly DateTimeOffset AsOf = DateTimeOffset.Parse("2026-10-16T18:00:00Z", CultureInfo.InvariantCulture);

    internal static LensBasis Basis { get; } = new("v2@a41b07d", Draft: false, AsOf);

    internal static GraphNode Node(string key, string label, Provenance provenance = Provenance.Human) => new(NodeKey.From(key), label, provenance);

    internal static GraphEdge Edge(string from, string to, EdgeType type, Provenance provenance = Provenance.Human, double confidence = 1.0) =>
        new(NodeKey.From(from), NodeKey.From(to), type, provenance, confidence);

    /// <summary>The sharing graph; the optional lists add nodes and edges.</summary>
    internal static GraphSnapshot Sharing(IEnumerable<GraphNode>? nodes = null, IEnumerable<GraphEdge>? edges = null) =>
        new(
            [
                Node("feature:sharing", "Sharing"), Node("feature:other", "Other"),
                Node("surface:screen/share-dialog", "Share dialog"),
                Node("action:sharing.create-link", "Create a link"), Node("action:sharing.resolve-link", "Resolve a link"),
                Node("thing:shared-link", "Shared link"), Node("thing:note", "Note"), Node("thing:ab", "Ab"),
                Node("code:src/sharing/links.ts", "src/sharing/links.ts"), Node("code:src/sharing/routes.ts", "src/sharing/routes.ts"),
                Node("code:tests/sharing.test.ts", "tests/sharing.test.ts"), Node("code:src/other/x.ts", "src/other/x.ts"),
                .. nodes ?? [],
            ],
            [
                Edge("surface:screen/share-dialog", "feature:sharing", EdgeType.PartOf),
                Edge("action:sharing.create-link", "surface:screen/share-dialog", EdgeType.On),
                Edge("action:sharing.resolve-link", "surface:screen/share-dialog", EdgeType.On),
                Edge("action:sharing.create-link", "thing:shared-link", EdgeType.Writes),
                Edge("action:sharing.resolve-link", "thing:shared-link", EdgeType.Reads),
                Edge("surface:screen/share-dialog", "thing:note", EdgeType.Reads),
                Edge("action:sharing.create-link", "thing:ab", EdgeType.Writes),
                Edge("code:src/sharing/links.ts", "feature:sharing", EdgeType.Implements),
                Edge("code:src/sharing/routes.ts", "surface:screen/share-dialog", EdgeType.Implements),
                Edge("code:tests/sharing.test.ts", "feature:sharing", EdgeType.Tests),
                Edge("code:src/other/x.ts", "feature:other", EdgeType.Implements),
                .. edges ?? [],
            ],
            [new NodeAlias(NodeKey.From("thing:short-url"), NodeKey.From("thing:shared-link"))],
            []);
}
