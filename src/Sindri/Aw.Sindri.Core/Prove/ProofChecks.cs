using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

/// <summary>
/// The proof pack's rules (line spec §7.3, PRV-*): pure decisions over <see cref="ProofFacts"/>. A rule whose inputs the facts do not carry is listed in
/// <see cref="ProofReport.NotRun"/> (PRV-ACC-001, PRV-SHA-001 and PRV-APP-001 until the evidence Step reads the evidence artifacts; PRV-HOLD-003 until the hold-out log records its
/// canary scan); the rest run on every call.
/// </summary>
public static partial class ProofChecks
{
    /// <summary>Runs every rule.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The failures and the rules that did not run.</returns>
    public static ProofReport Run(ProofFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var failures = new List<ProofFinding>();
        var notRun = new List<string>();
        Artifacts(facts, failures, notRun);
        Application(facts, notRun);
        BeforeAfter(facts, failures);
        Red(facts, failures);
        Holdout(facts, failures, notRun);
        failures.AddRange(ProofPack.AcceptanceFlips(facts.Acceptance, ProofPack.Acceptance(facts)));
        return new ProofReport(failures, notRun);
    }

    /// <summary>The ids of the phase's items that still need proving: every item that is not superseded.</summary>
    /// <param name="acceptance">The phase packet's acceptance.json.</param>
    /// <returns>The ids.</returns>
    public static IReadOnlyList<string> LiveItems(JsonNode acceptance) =>
        [.. (acceptance?["items"] as JsonArray ?? []).OfType<JsonObject>().Where(static i => (string?)i["status"] != "superseded").Select(static i => (string?)i["id"] ?? string.Empty)];

    private static void Artifacts(ProofFacts facts, List<ProofFinding> failures, List<string> notRun)
    {
        if (facts.Proof is null)
        {
            notRun.Add("PRV-ACC-001: fact:proof (the evidence artifacts) is not among the facts the evidence Step reads");
            notRun.Add("PRV-SHA-001: fact:proof (the evidence artifacts) is not among the facts the evidence Step reads");
            return;
        }

        foreach (var id in LiveItems(facts.Acceptance).Where(id => !ArtifactsOf(facts.Proof, id).Any(a => OnHead(a, facts.Head))))
        {
            failures.Add(new ProofFinding("PRV-ACC-001", id, $"{id}: no passing evidence ran on the head {Short(facts.Head)}", "run the evidence for this item on the current head"));
        }

        foreach (var (id, artifact) in AllArtifacts(facts.Proof).Where(a => (string?)a.Artifact["commit"] != facts.Head && (bool?)a.Artifact["superseded"] != true))
        {
            failures.Add(new ProofFinding("PRV-SHA-001", id, $"{id}: the artifact {(string?)artifact["path"]} ran on {Short((string?)artifact["commit"])}, not the head {Short(facts.Head)}, and is not marked superseded", "run it again on the head, or mark the older artifact superseded"));
        }
    }

    private static bool OnHead(JsonObject artifact, string head) =>
        (bool?)artifact["passing"] == true && (string?)artifact["commit"] == head && (bool?)artifact["superseded"] != true;

    private static IEnumerable<(string? Item, JsonObject Artifact)> AllArtifacts(JsonNode proof) =>
        (proof["items"] as JsonArray ?? []).OfType<JsonObject>().SelectMany(static i => (i["artifacts"] as JsonArray ?? []).OfType<JsonObject>().Select(a => ((string?)i["id"], a)));

    private static void Application(ProofFacts facts, List<string> notRun)
    {
        if (!Exempt(facts))
        {
            notRun.Add("PRV-APP-001: the hand test and verify results (fact:manualTest, fact:evidencePack) are not among the facts the evidence Step reads");
        }
    }

    private static void BeforeAfter(ProofFacts facts, List<ProofFinding> failures)
    {
        var results = (facts.BeforeAfter?["results"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        foreach (var (item, signal) in BaseAndHead(facts))
        {
            var result = results.Find(r => (string?)r["item"] == item && ((string?)r["signal"] is not { } s || string.Equals(s, signal, StringComparison.Ordinal)));
            if (BeforeAfterProblem(result) is { } problem)
            {
                failures.Add(new ProofFinding("PRV-BA-001", item, $"{item}: the {signal} check {problem}", "record the fix's check so it fails at the base and passes at the head, by the same script"));
            }
        }
    }

    private static string? BeforeAfterProblem(JsonObject? result)
    {
        if (result is null)
        {
            return "has no before/after result";
        }

        if ((string?)result["base"] != "fail")
        {
            return $"did not fail at the base ({(string?)result["base"] ?? "no result"})";
        }

        if ((string?)result["head"] != "pass")
        {
            return $"did not pass at the head ({(string?)result["head"] ?? "no result"})";
        }

        return result["baseScript"] is { } baseScript && result["headScript"] is { } headScript && (string?)baseScript != (string?)headScript ? "ran a different script at the head than at the base" : null;
    }

    private static IEnumerable<(string Item, string Signal)> BaseAndHead(ProofFacts facts) =>
        LiveItems(facts.Acceptance).SelectMany(id => (facts.Signals?["signals"]?[id] as JsonArray ?? []).OfType<JsonObject>()
            .Where(static s => (string?)s["runsAt"] == "base+head").Select(s => (id, (string?)s["id"] ?? string.Empty)));

    private static void Red(ProofFacts facts, List<ProofFinding> failures)
    {
        var tests = (facts.Red?["tests"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var exempt = Exempt(facts);
        if (tests.Count == 0 && !exempt)
        {
            failures.Add(new ProofFinding("PRV-RED-001", Item: null, "red.json is missing: no red test was recorded for this phase", "write the failing tests first and record them"));
        }

        foreach (var test in tests.Where(static t => (string?)t["outcome"] != "failed"))
        {
            failures.Add(new ProofFinding("PRV-RED-001", Item: null, $"the red test {(string?)test["id"]} did not fail before the change, so it proves nothing", "make the test fail for the reason the item names, then build"));
        }
    }

    private static IEnumerable<JsonObject> ArtifactsOf(JsonNode proof, string id) =>
        (proof["items"] as JsonArray ?? []).OfType<JsonObject>().Where(i => (string?)i["id"] == id).SelectMany(static i => (i["artifacts"] as JsonArray ?? []).OfType<JsonObject>());

    private static bool Exempt(ProofFacts facts) =>
        string.Equals(facts.PhaseKind, "docs", StringComparison.Ordinal) || string.Equals(facts.PhaseKind, "config", StringComparison.Ordinal);

    private static string Short(string? sha) => sha is { Length: > 12 } ? sha[..12] : sha ?? "none";
}
