using System.Text.Json;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Resolution;

namespace Aw.CodeIndex.Tests.Parsing;

// The quality probe (spec §11): the resolver's precision and recall on the 50 labeled pairs of this repo's own src/ at
// the commit they were labeled on. Reported, not gated; the owner has not reviewed the labels yet.
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
        var resolver = new CallResolver(index.Symbols, ManifestDirectories(commit));
        var edges = resolver.Resolve(index.CallSites);
        var lineOf = index.Parsed.ToDictionary(p => p.Ref, p => p.File);

        int truePositive = 0, falsePositive = 0, falseNegative = 0, trueNegative = 0;
        var rows = new List<string>();
        foreach (var pair in sample.RootElement.GetProperty("pairs").EnumerateArray())
        {
            var caller = pair.GetProperty("caller");
            var callee = pair.GetProperty("callee");
            var name = callee.GetProperty("name").GetString()!;
            var predicted = edges.FirstOrDefault(e =>
                e.TargetName == name && e.Line == caller.GetProperty("line").GetInt32() && lineOf[e.Source] == caller.GetProperty("file").GetString()
                && e.Target is { } target && lineOf[target] == callee.GetProperty("file").GetString()
                && index.KeyOf(target) == $"{callee.GetProperty("file").GetString()}:{callee.GetProperty("line").GetInt32()}");
            var label = pair.GetProperty("label").GetBoolean();
            if (label && predicted is not null)
            {
                truePositive++;
            }
            else if (label)
            {
                falseNegative++;
            }
            else if (predicted is not null)
            {
                falsePositive++;
            }
            else
            {
                trueNegative++;
            }

            rows.Add($"{pair.GetProperty("id").GetString()} {pair.GetProperty("weakSpot").GetString()} label={label} predicted={(predicted is null ? "no" : predicted.Confidence.ToString())}");
        }

        TestContext.Current.SendDiagnosticMessage(string.Join('\n', rows));
        TestContext.Current.SendDiagnosticMessage(
            $"resolver precision {Ratio(truePositive, truePositive + falsePositive):P0} ({truePositive} of {truePositive + falsePositive}), recall {Ratio(truePositive, truePositive + falseNegative):P0} ({truePositive} of {truePositive + falseNegative}), true negatives {trueNegative}");
        Assert.Equal(50, truePositive + falsePositive + falseNegative + trueNegative);
    }

    private static double Ratio(int part, int whole) => whole == 0 ? 0 : (double)part / whole;

    private static IReadOnlyCollection<string> ManifestDirectories(string commit) =>
        [.. FixtureSupport.CommitPaths(commit, "src", ".csproj").Select(p => p.Contains('/', StringComparison.Ordinal) ? p[..p.LastIndexOf('/')] : string.Empty)];
}
