using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Tests.Layout;

/// <summary>
/// Compares a produced JSON document with a golden: the same names, the same JSON types and nulls everywhere, and the same values
/// except under the paths named as wording (compared for type only; no array length is exempt). Array indexes in a path read as '[]' when matching.
/// </summary>
internal sealed partial class GoldenShapeDiff(IReadOnlyList<string> authored)
{
    public List<string> Between(JsonNode? expected, JsonNode? actual) => Compare(expected, actual, string.Empty);

    private List<string> Compare(JsonNode? expected, JsonNode? actual, string path)
    {
        if (expected is JsonObject golden && actual is JsonObject produced)
        {
            return Objects(golden, produced, path);
        }

        if (expected is JsonArray expectedItems && actual is JsonArray actualItems)
        {
            return Arrays(expectedItems, actualItems, path);
        }

        return Same(expected, actual, path) ? [] : [$"{path}: golden {Text(expected)}, produced {Text(actual)}"];
    }

    private bool Same(JsonNode? expected, JsonNode? actual, string path) =>
        expected?.GetValueKind() == actual?.GetValueKind() && (IsAuthored(path) || JsonNode.DeepEquals(expected, actual));

    private static string Text(JsonNode? node) => node?.ToJsonString() ?? "null";

    private List<string> Objects(JsonObject golden, JsonObject produced, string path)
    {
        var differences = new List<string>();
        foreach (var name in golden.Select(p => p.Key).Union(produced.Select(p => p.Key), StringComparer.Ordinal))
        {
            var at = path.Length == 0 ? name : $"{path}.{name}";
            if (!golden.ContainsKey(name))
            {
                differences.Add($"{at}: not in the golden");
            }
            else if (!produced.ContainsKey(name))
            {
                differences.Add($"{at}: missing from the produced output");
            }
            else
            {
                differences.AddRange(Compare(golden[name], produced[name], at));
            }
        }

        return differences;
    }

    private List<string> Arrays(JsonArray golden, JsonArray produced, string path)
    {
        if (golden.Count != produced.Count)
        {
            return [string.Create(CultureInfo.InvariantCulture, $"{path}: {golden.Count} items in the golden, {produced.Count} produced")];
        }

        return [.. golden.Zip(produced).SelectMany((pair, i) => Compare(pair.First, pair.Second, string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]")))];
    }

    private bool IsAuthored(string path)
    {
        var generic = Indexes.Replace(path, "[]");
        return authored.Any(a => string.Equals(generic, a, StringComparison.Ordinal) || generic.StartsWith(a + ".", StringComparison.Ordinal) || generic.StartsWith(a + "[", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"\[\d+\]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Indexes { get; }
}
