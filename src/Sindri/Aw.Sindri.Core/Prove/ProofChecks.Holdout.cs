using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

public static partial class ProofChecks
{
    private static void Holdout(ProofFacts facts, List<ProofFinding> failures, List<string> notRun)
    {
        var results = (facts.Holdout?["results"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (facts.HoldoutCount is not { } count)
        {
            notRun.Add("PRV-HOLD-001: the phase packet's holdout.json does not say how many hidden scenarios there are");
        }
        else
        {
            Ran(facts, results, count, failures);
        }

        var unspent = results.Count(static r => (bool?)r["spent"] != true);
        if (facts.HoldoutCount is not null && unspent < facts.HoldoutMin)
        {
            failures.Add(new ProofFinding("PRV-HOLD-002", Item: null, string.Create(CultureInfo.InvariantCulture, $"only {unspent} hidden scenarios were never spent; the phase needs {facts.HoldoutMin}"), "ask the owner for a fresh hidden set (sindri line holdout add)"));
        }

        switch ((string?)facts.Holdout?["canary"])
        {
            case "clean":
                break;
            case "leaked":
                failures.Add(new ProofFinding("PRV-HOLD-003", Item: null, "the canary token of the hidden set appears outside the store", "the owner replaces the hidden set; nothing the builder saw can stay"));
                break;
            default:
                notRun.Add("PRV-HOLD-003: the hold-out log has not recorded a canary scan (fact:holdoutResult has no 'canary')");
                break;
        }
    }

    private static void Ran(ProofFacts facts, List<JsonObject> results, int count, List<ProofFinding> failures)
    {
        if (results.Count < count)
        {
            failures.Add(new ProofFinding("PRV-HOLD-001", Item: null, string.Create(CultureInfo.InvariantCulture, $"{results.Count} of {count} hidden scenarios ran"), "run every hidden scenario of the phase"));
        }

        if (results.Count > 0 && (string?)facts.Holdout?["commit"] != facts.Head)
        {
            failures.Add(new ProofFinding("PRV-HOLD-001", Item: null, $"the hidden scenarios ran on {Short((string?)facts.Holdout?["commit"])}, not the head {Short(facts.Head)}", "run them again on the head"));
        }

        foreach (var failed in results.Where(static r => (string?)r["verdict"] != "pass"))
        {
            failures.Add(new ProofFinding("PRV-HOLD-001", (string?)failed["item"], $"{(string?)failed["item"]}: a hidden scenario did not pass", "fix what the finding from the hold-out log describes"));
        }
    }
}
