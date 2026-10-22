using System.Diagnostics;
using System.Globalization;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Features.BuildCodeIndex;

namespace Aw.CodeIndex.Tests.Build;

// First thing a person can run (spec §14 CI-6): the whole slice over this repository at its head, real git, Roslyn and index file.
public sealed class BuildThisRepoHeavyTests(ITestOutputHelper output)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "heavy")]
    public async Task This_repo_builds_and_callers_of_a_known_method_name_the_slice_that_calls_it()
    {
        var root = RepositoryRoot();
        var head = CommitSha.From(HeadOf(root));
        var repo = new CodeIndexRepo(RepoName.From("agentic-workflow"), root, Core.CodeIndexSettings.Default);
        using var index = new RealIndex();
        var lines = new Lines();

        var watch = Stopwatch.StartNew();
        var report = (await index.Service.Handle(new BuildCodeIndexRequest(repo, head, lines), Token)).Value;
        watch.Stop();

        output.WriteLine(Report(report, watch.ElapsedMilliseconds));
        foreach (var line in lines.All.Where(l => !l.StartsWith("structure ", StringComparison.Ordinal) || l.EndsWith(" files", StringComparison.Ordinal)).TakeLast(8))
        {
            output.WriteLine("  progress: " + line);
        }

        var scope = RealIndex.ScopeOf(repo);
        var plan = (await index.Facade.FindSymbols(scope, new SymbolQuery("Plan", SymbolMatch.Exact, Repo: null), Token)).Value.Hits.Single(h => h.File.EndsWith("Planning/BuildPlanner.cs", StringComparison.Ordinal));
        var callers = (await index.Facade.Callers(scope, plan.Ref, CallDepth.One, includeImplementations: false, Token)).Value;

        Assert.Equal(BuildOutcome.Published, report.Outcome);
        Assert.True(report.Counts.Files > 500, "files");
        Assert.True(report.Counts.Symbols > 5_000, "symbols");
        Assert.Contains(callers.Edges, e => e.Other?.File.EndsWith("BuildCodeIndex/BuildCodeIndexService.cs", StringComparison.Ordinal) == true);
        output.WriteLine($"Callers(BuildPlanner.Plan): {callers.Edges.Count} edges, e.g. {string.Join(", ", callers.Edges.Take(4).Select(e => e.Other?.File + ':' + e.Line.ToString(CultureInfo.InvariantCulture) + " " + e.Confidence))}");
    }

    private static string Report(BuildReport report, long milliseconds)
        => string.Create(CultureInfo.InvariantCulture, $"build report: outcome={report.Outcome} files={report.Counts.Files} symbols={report.Counts.Symbols} edges={report.Counts.Edges} unresolved={report.UnresolvedShare:P1} ambiguous={report.AmbiguousShare:P1} layers=[{string.Join(", ", report.Layers.Select(l => l.Layer + ":" + l.State))}] ms={report.Milliseconds} wall-ms={milliseconds}");

    private static string HeadOf(string root)
    {
        var start = new ProcessStartInfo("git", ["-C", root, "rev-parse", "HEAD"]) { RedirectStandardOutput = true, UseShellExecute = false };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git did not start.");
        var head = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return head;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root (AgenticWorkflow.slnx) is not above the test binaries.");
    }
}
