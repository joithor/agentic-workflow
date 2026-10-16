using System.Globalization;
using System.Text;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;

namespace Aw.Sindri.Host.Text;

/// <summary>The text form of a probe run: each probe's precision@5, the means, the baseline run beside them, and the verdict in words.</summary>
internal static class ContextEvalText
{
    internal static string Report(ProbeReport report, string? path)
    {
        var text = new StringBuilder(Invariant($"probes {report.ProbeFile} · map {report.MapVersion ?? "none"} · {report.Totals.Probes} probe(s)\n"));
        foreach (var probe in report.Probes)
        {
            var absent = probe.AbsentReturned.Count == 0 ? string.Empty : Invariant($" · ABSENT NODE RETURNED: {string.Join(", ", probe.AbsentReturned)}");
            var failed = probe.Error is { } code ? Invariant($" · query failed: {code}") : string.Empty;
            _ = text.AppendLine(Invariant($"  {probe.Id.Value,-24} precision@5 {probe.PrecisionAt5:0.00}  citations {probe.CitationCorrectness:0.00}{absent}{failed}"));
        }

        _ = text.AppendLine(Invariant($"mean precision@5 {report.Totals.PrecisionAt5:0.0000} · citation correctness {report.Totals.CitationCorrectness:0.0000}"));
        _ = text.AppendLine(report.Baseline is { } before
            ? Invariant($"baseline run {before.RunAt:O}: precision@5 {before.PrecisionAt5:0.0000} · citation correctness {before.CitationCorrectness:0.0000}")
            : "baseline run: none");
        if (report.Incomplete)
        {
            return Incomplete(text, report);
        }

        _ = text.AppendLine(report.Regressed ? "REGRESSED: SND-CONTEXT-038 (lower than the baseline run of this probe file, or a probe returned more absent nodes)"
            : report.Baseline is null ? "first run: nothing to compare" : "no regression");
        return text.Append("report ").AppendLine(path ?? "not written").ToString();
    }

    private static string Incomplete(StringBuilder text, ProbeReport report)
    {
        _ = text.AppendLine("INCOMPLETE: a probe's query failed, so this run says nothing about retrieval; it is not saved and not compared");
        foreach (var probe in report.Probes.Where(p => p.Error is not null))
        {
            _ = text.AppendLine(Invariant($"  failed probe {probe.Id.Value}: {probe.Error}"));
        }

        return text.AppendLine("report not saved").ToString();
    }

    /// <summary>The map's quality as text.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The text.</returns>
    internal static string Map(MapQualityRun run)
    {
        var r = run.Report;
        var text = new StringBuilder(Invariant($"map {r.MapVersion} quality\n"));
        _ = text.AppendLine(Invariant($"  node precision {Show(r.NodePrecision)} (deterministic {Show(r.NodePrecisionDeterministic)}, model {Show(r.NodePrecisionModel)}) · {r.Undecided} undecided"));
        _ = text.AppendLine(Invariant($"  link precision {Show(r.LinkPrecision)}"));
        _ = text.AppendLine(Invariant($"  unmapped rate {Show(r.UnmappedRate)}{(r.Window is null ? string.Empty : " over " + r.Window)}"));
        foreach (var bar in r.Bars)
        {
            _ = text.Append("  ").AppendLine(bar);
        }

        return text.Append("report ").AppendLine(run.Path).ToString();
    }

    /// <summary>What a purge removed.</summary>
    /// <param name="report">The counts.</param>
    /// <param name="all">True for 'purge --all'.</param>
    /// <returns>The text.</returns>
    internal static string Purge(EvalPurgeReport report, bool all) =>
        Invariant($"removed {report.Reports} report(s) and {report.CacheFiles} cache file(s){(all ? Invariant($" and {report.ProbeReports} probe report(s): the probe baselines are cleared too, so the next 'eval probes' says no baseline") : string.Empty)}\n");

    private static string Show(Ratio ratio) => ratio.Value is { } v ? Invariant($"{v:0.00} ({ratio.Hits}/{ratio.Total})") : "not measured";

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
