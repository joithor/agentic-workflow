using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    /// <summary>SCP-CHAL-001: the last challenger round reports nothing new and lists every lens and dimension it walked.</summary>
    private static void ChallengerDone(ScopeContext c, RuleSink s)
    {
        if (!s.Need("challenge"))
        {
            return;
        }

        var rounds = ScopeJson.Items(c.Facts["challenge"], "rounds").ToList();
        if (rounds.Count == 0)
        {
            s.Fail(item: null, "the challenger has not run", "Run the challenger; the last round must report nothing new.");
            return;
        }

        var last = rounds[^1];
        var label = string.Create(CultureInfo.InvariantCulture, $"round {rounds.Count}");
        if (!string.Equals(ScopeJson.Str(last, "verdict"), "nothingNew", StringComparison.Ordinal)
            || ScopeJson.Items(last, "surfaces").Any() || ScopeJson.Items(last, "behaviours").Any())
        {
            s.Fail(label, $"the last challenger round ({label}) found something new", "Fold the findings into the map, then run the challenger again until a round finds nothing new.");
        }

        var walked = ScopeJson.Items(last, "walked").ToList();
        var lenses = walked.Where(static w => string.Equals(ScopeJson.Str(w, "kind"), "lens", StringComparison.Ordinal)).Select(static w => ScopeJson.Str(w, "name")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var dimensions = walked.Where(static w => string.Equals(ScopeJson.Str(w, "kind"), "dimension", StringComparison.Ordinal)).Select(static w => ScopeJson.Str(w, "name")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var lens in ScopeRules.Lenses.Where(l => !lenses.Contains(l)))
        {
            s.Fail(label, $"the last challenger round did not walk the {lens} lens", "The challenger lists every lens and dimension it walked.");
        }

        foreach (var dimension in ScopeRules.Dimensions.Where(d => !dimensions.Contains(d)))
        {
            s.Fail(label, $"the last challenger round did not walk the dimension '{dimension}'", "The challenger lists every lens and dimension it walked.");
        }
    }

    /// <summary>SCP-CHAL-002: the challenger's model differs from the drafter's; a run at the desk is a warning (its model is self-reported), never a refusal.</summary>
    private static void ChallengerDiffers(ScopeContext c, RuleSink s)
    {
        if (!s.Need("runs"))
        {
            return;
        }

        var runs = (c.Facts["runs"] is JsonArray all ? all : ScopeJson.Arr(c.Facts["runs"], "runs")) ?? [];
        var drafter = LastRun(runs, "step.scope.draft");
        var challenger = LastRun(runs, "step.scope.challenge");
        if (drafter is null || challenger is null)
        {
            s.Fail(item: null, drafter is null ? "no run record for the drafter" : "no run record for the challenger", "Both agent runs leave a runtime record in runs.json.");
            return;
        }

        ModelsDiffer(drafter, challenger, s);
    }

    private static void ModelsDiffer(JsonNode drafter, JsonNode challenger, RuleSink s)
    {
        var challengerModel = ScopeJson.Str(challenger, "model");
        if (IsDesk(challenger) || IsDesk(drafter))
        {
            var self = string.IsNullOrEmpty(challengerModel) ? "model not recorded" : "model self-reported";
            s.Warn("step.scope.challenge", $"challenger ran at the desk: {self}", "Run the challenger on a second model for a recorded model.");
        }
        else if (string.IsNullOrEmpty(challengerModel) || string.Equals(ScopeJson.Str(drafter, "model"), challengerModel, StringComparison.Ordinal))
        {
            s.Fail("step.scope.challenge", $"the challenger ran on {challengerModel ?? "no recorded model"}, the drafter's model", "Run the challenger on a different model.");
        }
    }

    private static JsonNode? LastRun(JsonArray runs, string step) =>
        ScopeJson.Items(runs).LastOrDefault(r => string.Equals(ScopeJson.Str(r, "step"), step, StringComparison.Ordinal));

    private static bool IsDesk(JsonNode run) => string.Equals(ScopeJson.Str(run, "via"), "desk", StringComparison.Ordinal);
}
