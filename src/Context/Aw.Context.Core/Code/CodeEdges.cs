using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;

namespace Aw.Context.Core.Code;

/// <summary>The stored edges behind the code lens's lists, in the lenses' edge shape. Lexical matches emit none.</summary>
internal static class CodeEdges
{
    internal static List<LensEdge> Of(GraphSnapshot graph, HashSet<string> scope, List<CodeFile> files, List<CodeTest> tests, List<CodeThing> things)
    {
        var code = files.Select(f => "code:" + f.Path).Concat(tests.Select(t => "code:" + t.Path)).ToHashSet(StringComparer.Ordinal);
        var data = things.Select(t => t.Key.Value).ToHashSet(StringComparer.Ordinal);
        return [.. graph.Edges
            .Where(e => (e.Type is EdgeType.Implements or EdgeType.Tests && code.Contains(e.From.Value) && scope.Contains(e.To.Value))
                || (e.Type is EdgeType.Reads or EdgeType.Writes && scope.Contains(e.From.Value) && data.Contains(e.To.Value)))
            .OrderBy(e => e.Type).ThenBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal).Select(LensEdges.Of)];
    }
}
