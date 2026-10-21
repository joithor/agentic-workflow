using System.Globalization;
using System.Text.Json;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Resolution;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Tests.Parsing;

// The quality probe (spec §11): the resolver's precision and recall on the 50 labeled pairs of this repo's own src/ at
// the commit they were labeled on. Reported, not gated; the owner signed off the labels.
public sealed class ResolverQualityProbeTests
{
    [Fact, Trait("Lane", "heavy")]
    public void Precision_and_recall_on_the_fifty_pairs_are_reported()
    {
        using var sample = FixtureSupport.ReadJson("quality-sample/pairs.json");
        var commit = sample.RootElement.GetProperty("commit").GetString()!;
        if (!FixtureSupport.CommitExists(commit))
        {
            TestContext.Current.SendDiagnosticMessage($"quality probe skipped: commit {commit} is not in this clone");
            return;
        }

        var index = FixtureIndex.From(FixtureSupport.CommitSources(commit, "src"));
        var edges = new CallResolver(index.Symbols, ManifestDirectories(commit)).Resolve(index.CallSites);
        var fileOf = index.Parsed.ToDictionary(p => p.Ref, p => p.File);

        var tally = new Tally();
        var rows = new List<string>();
        foreach (var pair in sample.RootElement.GetProperty("pairs").EnumerateArray())
        {
            var predicted = Predicted(pair, edges, fileOf, index);
            var label = pair.GetProperty("label").GetBoolean();
            tally.Add(label, predicted is not null);
            rows.Add($"{pair.GetProperty("id").GetString()} {pair.GetProperty("weakSpot").GetString()} label={label} predicted={(predicted is null ? "no" : predicted.Confidence.ToString())}");
        }

        TestContext.Current.SendDiagnosticMessage(string.Join('\n', rows));
        TestContext.Current.SendDiagnosticMessage(tally.Report());
        Assert.Equal(50, tally.Total);
    }

    private static EdgeRecord? Predicted(JsonElement pair, IReadOnlyList<EdgeRecord> edges, Dictionary<SymbolRef, string> fileOf, FixtureIndex index)
    {
        var caller = pair.GetProperty("caller");
        var callee = pair.GetProperty("callee");
        var name = callee.GetProperty("name").GetString();
        var calleeFile = callee.GetProperty("file").GetString();
        var calleeKey = string.Create(CultureInfo.InvariantCulture, $"{calleeFile}:{callee.GetProperty("line").GetInt32()}");
        return edges.FirstOrDefault(e =>
            e.TargetName == name && e.Line == caller.GetProperty("line").GetInt32() && fileOf[e.Source] == caller.GetProperty("file").GetString()
            && e.Target is { } target && fileOf[target] == calleeFile && index.KeyOf(target) == calleeKey);
    }

    private static IReadOnlyCollection<string> ManifestDirectories(string commit) =>
        [.. FixtureSupport.CommitPaths(commit, "src", ".csproj").Select(p => p.Contains('/', StringComparison.Ordinal) ? p[..p.LastIndexOf('/')] : string.Empty)];

    private sealed class Tally
    {
        private int _truePositive;
        private int _falsePositive;
        private int _falseNegative;
        private int _trueNegative;

        public int Total => _truePositive + _falsePositive + _falseNegative + _trueNegative;

        public void Add(bool label, bool predicted)
        {
            switch (label, predicted)
            {
                case (true, true):
                    _truePositive++;
                    break;
                case (true, false):
                    _falseNegative++;
                    break;
                case (false, true):
                    _falsePositive++;
                    break;
                default:
                    _trueNegative++;
                    break;
            }
        }

        public string Report()
        {
            var predictedPositive = _truePositive + _falsePositive;
            var actualPositive = _truePositive + _falseNegative;
            return string.Create(CultureInfo.InvariantCulture, $"resolver precision {Ratio(_truePositive, predictedPositive):P0} ({_truePositive} of {predictedPositive}), recall {Ratio(_truePositive, actualPositive):P0} ({_truePositive} of {actualPositive}), true negatives {_trueNegative}");
        }

        private static double Ratio(int part, int whole) => whole == 0 ? 0 : (double)part / whole;
    }
}
