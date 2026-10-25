using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

public static partial class ProofChecks
{
    private static void Holdout(ProofFacts facts, List<ProofFinding> failures, List<string> notRun)
    {
        var results = HiddenResults(facts.Holdout);
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

    /// <summary>
    /// The hidden scenarios' results of a holdoutResult: the canary's is left out. A canary result has <c language="text">kind</c> 'canary' or no <c language="text">item</c> (the canary scenario shows no
    /// visible item, BS-6's <c language="text">HoldoutScenario.CanaryKind</c>); it is neither counted among the scenarios that ran nor judged nor spent. holdout.json's <c language="text">count</c> does not count it either.
    /// </summary>
    /// <param name="holdout">fact:holdoutResult.</param>
    /// <returns>The results of the hidden scenarios.</returns>
    internal static List<JsonObject> HiddenResults(JsonNode? holdout) =>
        [.. (holdout?["results"] as JsonArray ?? []).OfType<JsonObject>().Where(static r => (string?)r["kind"] != "canary" && r["item"] is not null)];

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
