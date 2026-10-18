using System.Text.Json.Nodes;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The gold facts and the line gold scope packet (tests/fixtures/line/gold/scope/expiry) are one document: the facts equal the packet with the stamped fields removed, so neither drifts alone.</summary>
public sealed class ScopingGoldFactsTests
{
    private static readonly string _gold = Path.Combine(RepoRoot(), "tests", "fixtures", "line", "gold", "scope", "expiry");

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }

    private static readonly string[] _stamped = ["trust", "schemaVersion"];

    private static JsonNode? Strip(JsonNode? node, bool top)
    {
        if (node is JsonObject o)
        {
            foreach (var name in _stamped)
            {
                _ = o.Remove(name);
            }

            if (top)
            {
                _ = o.Remove("change");   // the packet names its change and base commit; the facts keep them in brief
                _ = o.Remove("baseSha");
            }

            if (o["parent"] is null)
            {
                _ = o.Remove("parent");   // a null parent is how the packet says "none"
            }

            foreach (var (_, child) in o.ToList())
            {
                _ = Strip(child, top: false);
            }
        }
        else if (node is JsonArray a)
        {
            foreach (var child in a)
            {
                _ = Strip(child, top: false);
            }
        }

        return node;
    }

    [Theory, Trait("Lane", "integration")]   // reads the line fixture from the source tree
    [InlineData("scopeMap", "scope-map.json")]
    [InlineData("acceptance", "acceptance.json")]
    [InlineData("coverage", "coverage.json")]
    [InlineData("challenge", "challenge.json")]
    [InlineData("lenses", "lenses.json")]
    public async Task The_facts_equal_the_line_gold_packet_without_its_stamped_fields(string fact, string file)
    {
        var gold = Strip(JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(_gold, file), TestContext.Current.CancellationToken)), top: true)!;
        var facts = Strip(JsonNode.Parse(ScopingFacts.Expiry()[fact]), top: true)!;
        Assert.Equal(gold.ToJsonString(), facts.ToJsonString());
    }
}
