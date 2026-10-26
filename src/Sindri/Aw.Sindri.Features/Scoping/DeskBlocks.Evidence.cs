using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The evidence Step's code: the packet's checks, and the outcomes 'complete', 'converged', 'incomplete' and 'regather'.</summary>
internal static partial class DeskBlocks
{
    /// <summary>'fact:recommendation' for the owner's stop: approve when no question is left open, else revise (the stop's form shows it as the recommended outcome).</summary>
    private static JsonObject Recommendation(JsonNode openQuestions)
    {
        var open = ScopeJson.Items(openQuestions).Count();
        return new JsonObject
        {
            ["outcome"] = open == 0 ? "approve" : "revise",
            ["why"] = open == 0 ? "Every check passed and no question is left open." : string.Create(CultureInfo.InvariantCulture, $"{open} question(s) are still open."),
        };
    }

    private static Result<ScopingDeskResponse> Evidence(ScopeFacts facts, int challengeCap, int gapCap)
    {
        var assembled = ScopeEvidence.FlagUncited(facts);
        var report = ScopePacketChecks.Run(assembled, ScopeStage.Evidence, challengeCap, gapCap);
        if (LensFaultAt("step.scope.evidence", report) is { } lens)
        {
            return lens;
        }

        if (report.Outcome is not ScopeOutcome.Complete)
        {
            var outcome = report.Outcome == ScopeOutcome.Regather ? "regather" : "incomplete";
            var missing = report.Rules.Where(static r => r.Status == ScopeRuleStatus.NotRun).Select(static r => r.Rule).ToList();
            var findings = report.Findings.Concat(missing.Select(static r => new ScopeFinding(r, Item: null, $"{r} did not run: a fact it needs is missing", "Write the missing facts into the Step directory.", ScopeSeverity.Fail))).ToList();
            var log = RoundLog.Append(facts, "evidence", "step.scope.evidence", findings);
            if (outcome == "incomplete" && RoundLog.Repeats(facts, "evidence", findings) && assembled.Missing("decision", "questions").Count == 0)
            {
                return Converged(assembled, findings, log, report);
            }

            return Reply("step.scope.evidence", outcome, Outputs(("findings", FindingsFact(findings)), (RoundLog.Key, log)), [], report);
        }

        if (assembled.Missing("decision", "questions") is { Count: > 0 } lacking)
        {
            return SindriErrors.InvalidInput.With($"evidence assembles size.json and questions.json from {string.Join(", ", lacking)} (fix: write them into the facts directory)");
        }

        var outputs = Outputs(
            ("scopeMap", assembled.Require("scopeMap")),
            ("acceptance", assembled.Require("acceptance")),
            ("coverage", assembled.Require("coverage")),
            ("openQuestions", assembled.Require("openQuestions")),
            ("recommendation", Recommendation(assembled.Require("openQuestions"))));
        if (assembled["runs"] is { } runs)
        {
            // why: the runs the model checks compared are the ones the packet's runs.json holds, and the freeze re-checks them from this fact
            outputs["runs"] = ScopeJson.Write(runs);
        }

        return Reply("step.scope.evidence", "complete", outputs, ScopePacket.Build(assembled), report);
    }

    /// <summary>
    /// Two evidence rounds in a row found only the same things (#1120): another draft round would find them again, so the owner decides. The packet is assembled as on 'complete' (the stop reads it)
    /// and the findings stay open in 'fact:findings' and the round log; the recommendation is to revise, and the freeze re-checks the packet, so nothing is approved around a failing rule.
    /// </summary>
    private static Result<ScopingDeskResponse> Converged(ScopeFacts assembled, List<ScopeFinding> findings, JsonArray log, ScopingCheckResponse report)
    {
        var open = string.Create(CultureInfo.InvariantCulture, $"{findings.Count} finding(s) are still open after two evidence rounds that found the same things.");
        var outputs = Outputs(
            ("scopeMap", assembled.Require("scopeMap")),
            ("acceptance", assembled.Require("acceptance")),
            ("coverage", assembled.Require("coverage")),
            ("openQuestions", assembled.Require("openQuestions")),
            ("recommendation", new JsonObject { ["outcome"] = "revise", ["why"] = open }),
            ("findings", FindingsFact(findings)),
            (RoundLog.Key, log));
        if (assembled["runs"] is { } runs)
        {
            outputs["runs"] = ScopeJson.Write(runs);   // why: the freeze re-checks the model checks from this fact, as after 'complete'
        }

        return Reply("step.scope.evidence", "converged", outputs, ScopePacket.Build(assembled), report);
    }
}
