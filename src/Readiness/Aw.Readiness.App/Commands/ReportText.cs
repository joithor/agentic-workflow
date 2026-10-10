using System.Globalization;
using System.Text;
using Aw.Readiness.App.Features.CheckRepo;
using Aw.Readiness.Core;
using Aw.Readiness.Core.Scoring;

namespace Aw.Readiness.App.Commands;

/// <summary>The text report: words for every state (colour never carries meaning, so NO_COLOR has nothing to strip).</summary>
internal static class ReportText
{
    internal static string Render(CheckRepoResponse report)
    {
        var text = new StringBuilder();
        _ = text.Append(Inv($"aw-readiness: {report.Repo} ({report.Scope})\n"))
            .Append(Inv($"Level {report.Level} of {ReadinessScoring.MaxLevel}; target {report.Target.Level} (ring {report.Target.Ring}): "))
            .Append(report.Holds ? "holds\n" : "does not hold\n")
            .Append(Delta(report)).Append('\n');
        Pillars(text, report);
        Findings(text, report.Findings);
        return text.ToString();
    }

    private static string Delta(CheckRepoResponse report)
    {
        var delta = report.Delta;
        if (delta.IsFirstRun)
        {
            return $"first run: baseline recorded ({report.RecordedTo})\n";
        }

        var since = delta.Since!.Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);   // why: not a first run
        if (delta.Changes.Count == 0)
        {
            return Inv($"since {since}: no change (level {delta.LevelAfter})\n");
        }

        var noun = delta.Changes.Count == 1 ? "change" : "changes";
        var before = delta.LevelBefore?.ToString(CultureInfo.InvariantCulture) ?? "-";
        var text = new StringBuilder(Inv($"since {since}: level {before} -> {delta.LevelAfter}, {delta.Changes.Count} {noun}\n"));
        foreach (var change in delta.Changes)
        {
            _ = text.Append(Inv($"  {change.Id}: {Word(change.Before)} -> {Word(change.After)}\n"));
        }

        return text.ToString();
    }

    private static void Pillars(StringBuilder text, CheckRepoResponse report)
    {
        _ = text.Append("pillar                       L1      L2      L3      L4      L5      level\n");
        foreach (var pillar in report.Pillars)
        {
            _ = text.Append(Name(pillar.Pillar).PadRight(29));
            foreach (var tally in pillar.Tallies)
            {
                _ = text.Append((tally.IsEmpty ? "-" : Inv($"{tally.Passed}/{tally.Scored}")).PadRight(8));
            }

            _ = text.Append(pillar.Level is { } level ? Inv($"{level}\n") : "-\n");
        }

        _ = text.Append("overall".PadRight(29));
        foreach (var tally in report.Tallies)
        {
            _ = text.Append((tally.IsEmpty ? "-" : Inv($"{tally.Passed}/{tally.Scored}")).PadRight(8));
        }

        _ = text.Append(Inv($"{report.Level}\n\n"));
    }

    private static void Findings(StringBuilder text, IReadOnlyList<Finding> findings)
    {
        foreach (var f in findings)
        {
            var state = f.State == CriterionState.Fail ? "FAIL" : Word(f.State);
            _ = text.Append(Inv($"{state}  {f.Criterion.Id} (L{f.Criterion.Level}) {f.Criterion.Title}: {f.Evidence}\n"));
            if (f.State == CriterionState.Fail)
            {
                _ = text.Append("      fix: ").Append(f.Criterion.Fix).Append('\n');
            }
        }
    }

    private static string Word(CriterionState? state) => state switch
    {
        CriterionState.Pass => "pass",
        CriterionState.Fail => "fail",
        CriterionState.NotApplicable => "n/a",
        CriterionState.Unknown => "unknown",
        _ => "absent",
    };

    private static string Name(Pillar pillar) => pillar switch
    {
        Pillar.StyleAndValidation => "Style & Validation",
        Pillar.BuildSystem => "Build System",
        Pillar.Testing => "Testing",
        Pillar.Documentation => "Documentation",
        Pillar.DevelopmentEnvironment => "Development Environment",
        Pillar.DebuggingAndObservability => "Debugging & Observability",
        Pillar.Security => "Security",
        Pillar.TaskDiscovery => "Task Discovery",
        Pillar.ProductAndExperimentation => "Product & Experimentation",
        _ => pillar.ToString(),
    };

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
