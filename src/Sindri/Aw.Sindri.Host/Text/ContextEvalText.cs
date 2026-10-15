using System.Globalization;
using System.Text;
using Aw.Context.Core.Eval;

namespace Aw.Sindri.Host.Text;

/// <summary>The text form of a probe run: each probe's precision@5, the means, the previous run beside them, and the verdict in words.</summary>
internal static class ContextEvalText
{
    internal static string Report(ProbeReport report, string path)
    {
        var text = new StringBuilder(Invariant($"probes {report.ProbeFile} · map {report.MapVersion ?? "none"} · {report.Totals.Probes} probe(s)\n"));
        foreach (var probe in report.Probes)
        {
            var absent = probe.AbsentReturned.Count == 0 ? string.Empty : Invariant($" · ABSENT NODE RETURNED: {string.Join(", ", probe.AbsentReturned)}");
            var failed = probe.Error is { } code ? Invariant($" · query failed: {code}") : string.Empty;
            _ = text.AppendLine(Invariant($"  {probe.Id.Value,-24} precision@5 {probe.PrecisionAt5:0.00}  citations {probe.CitationCorrectness:0.00}{absent}{failed}"));
        }

        _ = text.AppendLine(Invariant($"mean precision@5 {report.Totals.PrecisionAt5:0.0000} · citation correctness {report.Totals.CitationCorrectness:0.0000}"));
        _ = text.AppendLine(report.Previous is { } before
            ? Invariant($"previous run {before.RunAt:O}: precision@5 {before.PrecisionAt5:0.0000} · citation correctness {before.CitationCorrectness:0.0000}")
            : "previous run: none");
        _ = text.AppendLine(report.Regressed ? "REGRESSED: SND-CONTEXT-038 (lower than the previous run of this probe file)"
            : report.Previous is null ? "first run: nothing to compare" : "no regression");
        return text.Append("report ").AppendLine(path).ToString();
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
