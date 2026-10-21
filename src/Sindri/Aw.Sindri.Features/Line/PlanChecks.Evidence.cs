using System.Globalization;

namespace Aw.Sindri.Features.Line;

internal static partial class PlanChecks
{
    // PLN-EXEC-001: the last executability review reports 'executable' for every phase.
    private static IEnumerable<LineFinding> Executable(PlanContext c)
    {
        var verdicts = LineJson.Items(c.Plan, "execReview").ToList();
        foreach (var phase in c.Phases)
        {
            var id = PlanContext.Id(phase);
            var last = verdicts.FindLast(v => string.Equals(LineJson.Str(v, "phase"), id, StringComparison.Ordinal));
            if (last is null || !string.Equals(LineJson.Str(last, "verdict"), "executable", StringComparison.Ordinal))
            {
                yield return DesignContext.Fail("PLN-EXEC-001", id, $"the executability review does not report phase {id} executable", "Resolve its objections and review again.");
            }
        }
    }

    // PLN-HOLD-003: every phase has a holdout set of at least holdoutMin scenarios and a canary, by hash.
    private static IEnumerable<LineFinding> Holdouts(PlanContext c)
    {
        var min = LineJson.Int(c.Plan, "holdoutMin") ?? 2;
        foreach (var phase in c.Phases)
        {
            var id = PlanContext.Id(phase);
            var set = c.HoldoutSets.FirstOrDefault(s => string.Equals(LineJson.Str(s, "phase"), id, StringComparison.Ordinal));
            if (set is null || (LineJson.Int(set, "count") ?? 0) < min || string.IsNullOrWhiteSpace(LineJson.Str(set, "hash")) || LineJson.Bool(set, "canary") != true)
            {
                yield return DesignContext.Fail("PLN-HOLD-003", id, $"phase {id} has no holdout set of at least {min.ToString(CultureInfo.InvariantCulture)} scenarios with a canary, by hash", "Write the phase's hidden scenarios and its canary.");
            }
        }
    }
}
