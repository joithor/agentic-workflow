using System.Globalization;
using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Features.RunBacktest;

/// <summary>Turns the scores into the report (spec §9.1, §9.4). Pure.</summary>
internal static class ReportAssembly
{
    internal static BacktestReport Assemble(BacktestRequest request, (DateTimeOffset Started, LeftOut LeftOut) run, Collected collected)
    {
        var kept = collected.Kept;
        var briefScore = collected.Scores.TryGetValue(Arm.Brief, out var brief) ? BacktestScoring.Aggregate(brief, tokens: 0) : null;
        var rows = new List<ArmReport>();
        foreach (var arm in Arms.Available.Where(request.Arms.Contains).Concat(Arms.NotAvailable))   // the arms not available yet always print their row (spec §9.3)
        {
            if (Arms.PendingStage(arm) is { } stage)
            {
                rows.Add(new ArmReport(Arms.Name(arm), "source", Leaky: false, stage, Score: null, PassLine: null, []));
                continue;
            }

            var scores = collected.Scores[arm];
            var score = BacktestScoring.Aggregate(scores, tokens: 0);
            rows.Add(new ArmReport(Arms.Name(arm), "source", arm == Arm.GraphReviewed, NotAvailable: null, score, PassLine.Evaluate(score, arm == Arm.Brief ? null : briefScore, kept.Count), scores));
        }

        var judged = rows.Where(r => r.PassLine is not null).ToList();
        var overall = (judged.FirstOrDefault(r => r.Arm == Arms.Name(Arm.All)) ?? judged.LastOrDefault())?.PassLine?.Overall;
        return new BacktestReport(BacktestReport.CurrentSchemaVersion, run.Started, Window(request), kept.Count, kept.Count < PassBars.MinSamples,
            kept.Count == 0 ? 0 : Math.Round((double)kept.Count(s => s.Leaky) / kept.Count, 4, MidpointRounding.ToEven), run.LeftOut, kept, rows, overall, []);
    }

    private static string Window(BacktestRequest request)
    {
        var range = request.Since is null && request.Until is null
            ? "all"
            : string.Create(CultureInfo.InvariantCulture, $"{request.Since:yyyy-MM-dd}..{request.Until:yyyy-MM-dd}");
        return request.Limit is { } limit ? string.Create(CultureInfo.InvariantCulture, $"{range}, first {limit}") : range;
    }
}
