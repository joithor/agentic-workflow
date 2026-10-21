using System.Globalization;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Resolution;

namespace Aw.CodeIndex.Tests.Parsing;

public sealed class FixtureGoldenTests
{
    private static readonly string[] _kindWords = ["function", "method", "constructor", "accessor", "local-function", "lambda", "class", "record", "struct", "interface", "enum", "graph-symbol"];

    private static FixtureIndex ParseProject()
    {
        var project = Path.Combine(FixtureSupport.FixtureRoot(), "project");
        var sources = Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(project, f).Replace('\\', '/'), File.ReadAllText, StringComparer.Ordinal);
        return FixtureIndex.From(sources);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_fixture_project_parses_to_exactly_the_expected_symbols()
    {
        using var expected = FixtureSupport.ReadJson("expected.json");
        var index = ParseProject();

        var want = expected.RootElement.GetProperty("symbols").EnumerateArray().Select(s =>
            string.Create(CultureInfo.InvariantCulture, $"{s.GetProperty("key").GetString()} | {s.GetProperty("qualified").GetString()} | {s.GetProperty("kind").GetString()} | {s.GetProperty("arity").GetInt32()} | {s.GetProperty("startLine").GetInt32()}-{s.GetProperty("endLine").GetInt32()} | {s.GetProperty("exported").GetBoolean()} | {s.GetProperty("signature").GetString()}")).Order(StringComparer.Ordinal);
        var got = index.Parsed.Select(p =>
            string.Create(CultureInfo.InvariantCulture, $"{p.File}:{p.Symbol.StartLine} | {p.Symbol.Qualified} | {_kindWords[(int)p.Symbol.Kind]} | {p.Symbol.Arity} | {p.Symbol.StartLine}-{p.Symbol.EndLine} | {p.Symbol.Exported} | {p.Symbol.Signature}")).Order(StringComparer.Ordinal);

        Assert.Equal(want, got);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_parsed_call_sites_resolve_to_exactly_the_expected_edges()
    {
        using var expected = FixtureSupport.ReadJson("expected.json");
        var index = ParseProject();

        var edges = new CallResolver(index.Symbols, [""]).Resolve(index.CallSites);

        var want = expected.RootElement.GetProperty("edges").EnumerateArray().Select(e =>
            Row(e.GetProperty("src").GetString()!, e.GetProperty("kind").GetString()!, e.GetProperty("name").GetString()!, e.GetProperty("line").GetInt32(),
                e.GetProperty("confidence").GetString()!, e.GetProperty("candidates").GetInt32(), e.GetProperty("dst").EnumerateArray().Select(d => d.GetString()!))).Order(StringComparer.Ordinal);
        var got = edges.GroupBy(e => (e.Source, e.TargetName, e.Kind, e.Line, e.Confidence, e.Candidates)).Select(g =>
            Row(index.KeyOf(g.Key.Source), g.Key.Kind == EdgeKind.Calls ? "calls" : "inherits", g.Key.TargetName, g.Key.Line,
                g.Key.Confidence.ToString().ToLowerInvariant(), g.Key.Candidates, g.Where(e => e.Target is not null).Select(e => index.KeyOf(e.Target!.Value)))).Order(StringComparer.Ordinal);

        Assert.Equal(want, got);
    }

    [Fact, Trait("Lane", "integration")]
    public void Every_confidence_of_the_resolver_occurs_in_the_fixture()
    {
        var index = ParseProject();

        var confidences = new CallResolver(index.Symbols, [""]).Resolve(index.CallSites).Select(e => e.Confidence).Distinct().Order();

        Assert.Equal([Confidence.Certain, Confidence.Likely, Confidence.Ambiguous, Confidence.Unresolved], confidences);
    }

    private static string Row(string src, string kind, string name, int line, string confidence, int candidates, IEnumerable<string> dst) =>
        string.Create(CultureInfo.InvariantCulture, $"{src} | {kind} {name} @{line} | {confidence} of {candidates} | {string.Join(", ", dst)}");
}
