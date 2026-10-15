using System.Globalization;
using System.Text;
using Aw.Context.Core.Eval;

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

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
