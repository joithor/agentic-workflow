using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Commands.Probe;

namespace Aw.Sindri.Tests.Commands.Probe;

/// <summary>
/// The pinned harness probe against its goldens (engine spec §6.2, §10): every harness has a golden under Golden/Probe/,
/// either the report 'sindri doctor --probe' printed inside it or 'pending: owner probe', and the lists the CLI reads
/// are what those reports say.
/// </summary>
public sealed class HarnessProbeGoldenTests
{
    private const string Directory = "Golden/Probe";

    [Fact, Trait("Lane", "unit")]
    public void Agent_variables_match_the_probe()
    {
        var humans = PinnedProbe.All.Where(h => h.Human && !h.Pending).Select(h => Report(h)!).ToList();
        var humanVariables = humans.SelectMany(r => r.Variables).ToHashSet(StringComparer.Ordinal);

        foreach (var pinned in PinnedProbe.All)
        {
            if (pinned.Pending)
            {
                Assert.Equal(PinnedHarness.PendingNote, Pending(pinned));
                continue;
            }

            var report = Report(pinned)!;
            Assert.Equal((pinned.Harness, pinned.Version), (report.Harness, report.Version));
            if (pinned.Human)
            {
                Assert.Empty(AgentVariables.Intersect(report.Variables, StringComparer.Ordinal));
                Assert.DoesNotContain(report.Ancestry, a => PinnedProbe.Processes.Any(p => p.Matches(a)));
                continue;
            }

            Assert.All(pinned.Markers, m => Assert.Contains(m, report.Variables, StringComparer.Ordinal));
            Assert.Empty(pinned.Markers.Intersect(humanVariables, StringComparer.Ordinal));
            Assert.Equal(
                report.Variables.Except(humanVariables, StringComparer.Ordinal).Except(pinned.Markers, StringComparer.Ordinal).Order(StringComparer.Ordinal),
                pinned.Excluded.Keys.Order(StringComparer.Ordinal));
            Assert.All(pinned.Processes, p => Assert.Contains(report.Ancestry, p.Matches));
            Assert.All(humans, h => Assert.DoesNotContain(h.Ancestry, a => pinned.Processes.Any(p => p.Matches(a))));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_harness_has_a_golden_and_every_golden_a_harness()
    {
        var files = System.IO.Directory.GetFiles(Golden.SourcePath(typeof(HarnessProbeGoldenTests).Assembly, Directory), "*.json")
            .Select(Path.GetFileName).Order(StringComparer.Ordinal);

        Assert.Equal(PinnedProbe.All.Select(h => h.GoldenFile).Order(StringComparer.Ordinal), files);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_prover_unsets_every_agent_variable_in_human_mode()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "sindri-prove.sh"));
        var line = script.Split('\n').Single(l => l.Contains("AW_SINDRI_PROVER=1 \\", StringComparison.Ordinal));

        Assert.All(AgentVariables, v => Assert.Contains($"-u {v} ", line, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_host_smoke_unsets_every_agent_variable()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Sindri", "aot-smoke", "run.sh"));
        var unset = script.Split('\n').Single(l => l.StartsWith("unset ", StringComparison.Ordinal)).Split(' ').Skip(1).ToHashSet(StringComparer.Ordinal);

        Assert.All(AgentVariables, v => Assert.Contains(v, unset));
    }

    /// <summary>The variables the proof scripts unset: the pinned probe's markers, then Sindri's own recursion guard.</summary>
    private static IReadOnlyList<string> AgentVariables { get; } = [.. PinnedProbe.Variables, "AW_SINDRI_CHILD"];

    private static string Text(PinnedHarness pinned) =>
        File.ReadAllText(Golden.SourcePath(typeof(HarnessProbeGoldenTests).Assembly, $"{Directory}/{pinned.GoldenFile}"));

    private static string? Pending(PinnedHarness pinned)
    {
        using var document = JsonDocument.Parse(Text(pinned));
        return document.RootElement.TryGetProperty("pending", out var note) ? note.GetString() : null;
    }

    private static HarnessProbeReport? Report(PinnedHarness pinned) =>
        AwJson.Deserialize<HarnessProbeReport>(System.Text.Encoding.UTF8.GetBytes(Text(pinned)), SliceHarness.Json).Value;

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No AgenticWorkflow.slnx above the test's base directory.");
    }
}
