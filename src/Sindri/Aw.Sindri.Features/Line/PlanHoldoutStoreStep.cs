using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'step.plan.holdout-store' on the block runtime (line spec §9.1, LN-WF4): moves the hidden scenarios the author Step wrote into its run folder into the holdout store, where the evaluator and
/// holdout-log find them, and reports only ids, counts and hashes. The author's file, <c language="text">holdout-sets.json</c> in the directory <c language="text">fact:holdoutDraft</c> names,
/// is <c language="text">{ "sets": [ { "phase": "&lt;plan phase id&gt;", "scenarios": [ { id, item, kind, steps[], expected } ] } ] }</c>. Every phase of the plan needs a set of at least
/// the plan's <c language="text">holdoutMin</c> (default 2) scenarios, each naming a visible acceptance item of its phase; a set that doesn't is 'invalid' with findings and goes back to the author.
/// Sindri adds the canary scenario itself, with a random phrase the author never sees, and stores each set under the phase packet's id (the key holdout-log looks it up by).
/// </summary>
/// <param name="packets">Hashes and checks the scope packet.</param>
/// <param name="packetFiles">The scope packet's files.</param>
/// <param name="drafts">Reads the author's file.</param>
/// <param name="store">The holdout store.</param>
internal sealed class PlanHoldoutStoreStep(IPackets packets, IPacketFiles packetFiles, IBriefFiles drafts, IHoldoutStore store) : IBlockStep, IBlockStepId
{
    private const string DraftName = "holdout-sets.json";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.plan.holdout-store");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var plan = StepFacts.Read(context, "fact:plan");
        if (context.Address?.Split('/')[0] is not { Length: > 0 } line || LineJson.Str(StepFacts.Read(context, "fact:holdoutDraft"), "dir") is not { Length: > 0 } dir)
        {
            return BlockStepResult.Failed($"{Id.Value} needs the instance's address and fact:holdoutDraft naming the author's directory");
        }

        var scope = await LineScope.Load(packets, packetFiles, StepFacts.Read(context, "fact:scopePacket"), cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return BlockStepResult.Failed($"{Id.Value}: {scope.Error.Message}");
        }

        var draft = await drafts.Read(DraftName, [dir], cancellationToken).ConfigureAwait(false);
        if (draft.IsError)
        {
            return Invalid($"the author wrote no {DraftName} in {dir}: {draft.Error.Message}", "Write holdout-sets.json in the run folder and report again.");
        }

        var min = LineJson.Int(plan, "holdoutMin") ?? 2;
        var (sets, found) = Parse(draft.Value.Text, [.. LineJson.Items(plan, "phases")], scope.Value, min);
        if (found.Count > 0)
        {
            return LinePackets.Gaps("invalid", found);
        }

        return await Save(line, sets, cancellationToken).ConfigureAwait(false);
    }

    private static BlockStepResult Invalid(string message, string fix) => LinePackets.Gaps("invalid", [LinePackets.Finding("PLN-HOLD-003", message, fix)]);

    private async ValueTask<BlockStepResult> Save(string line, List<Prepared> sets, CancellationToken cancellationToken)
    {
        var report = new List<JsonNode?>();
        foreach (var set in sets)
        {
            var saved = await store.Save(line, set.Key, set.Text, cancellationToken).ConfigureAwait(false);
            if (saved.IsError)
            {
                return BlockStepResult.Failed($"{Id.Value} can't store the hidden set of phase {set.Phase}: {saved.Error.Message}");
            }

            report.Add(new JsonObject { ["phase"] = set.Phase, ["count"] = set.Count, ["hash"] = saved.Value.Hash, ["canary"] = true });
        }

        return StepFacts.Done("stored", ("fact:holdoutSets", new JsonArray([.. report])));
    }

    private static (List<Prepared> Sets, List<LineFinding> Findings) Parse(string text, List<JsonNode> phases, JsonObject scope, int min)
    {
        var findings = new List<LineFinding>();
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(text);
        }
        catch (JsonException e)
        {
            return ([], [LinePackets.Finding("PLN-HOLD-003", $"{DraftName} is not JSON: {e.Message}", "Write the file as { \"sets\": [ { \"phase\", \"scenarios\" } ] }.")]);
        }

        var drafted = LineJson.Items(root, "sets").ToList();
        var prepared = new List<Prepared>();
        foreach (var phase in phases)
        {
            var id = PlanContext.Id(phase);
            var items = PlanContext.Items(phase).Select(static i => LineJson.Str(i, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
            var set = drafted.Find(d => string.Equals(LineJson.Str(d, "phase"), id, StringComparison.Ordinal));
            if (set is null)
            {
                findings.Add(LinePackets.Finding("PLN-HOLD-003", $"{DraftName} has no set for phase {id}", "Write the phase's hidden scenarios."));
                continue;
            }

            var problems = Check([.. LineJson.Items(set, "scenarios")], items, id, min);
            findings.AddRange(problems);
            if (problems.Count == 0)
            {
                prepared.Add(Prepare(id, PhasePackets.Id(scope, id), LineJson.Items(set, "scenarios")));
            }
        }

        findings.AddRange(drafted
            .Where(d => !phases.Exists(p => string.Equals(PlanContext.Id(p), LineJson.Str(d, "phase"), StringComparison.Ordinal)))
            .Select(d => LinePackets.Finding("PLN-HOLD-003", $"{DraftName} has a set for phase {LineJson.Str(d, "phase") ?? "(none)"}, which the plan does not have", "Remove it.")));
        return (prepared, findings);
    }

    private static List<LineFinding> Check(List<JsonNode> scenarios, HashSet<string> items, string phase, int min)
    {
        var findings = new List<LineFinding>();
        var author = scenarios.Where(static s => !string.Equals(LineJson.Str(s, "kind"), HoldoutScenario.CanaryKind, StringComparison.Ordinal)).ToList();
        if (author.Count < min)
        {
            findings.Add(LinePackets.Finding("PLN-HOLD-003", $"phase {phase} has {author.Count.ToString(CultureInfo.InvariantCulture)} hidden scenarios, not the {min.ToString(CultureInfo.InvariantCulture)} it needs", "Write more hidden scenarios."));
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in author)
        {
            var key = LineJson.Str(s, "id");
            var problem = Problem(s, key, items, seen);
            if (problem is not null)
            {
                findings.Add(LinePackets.Finding("PLN-HOLD-003", $"phase {phase}: {problem}", "Fix the scenario in holdout-sets.json."));
            }
        }

        return findings;
    }

    private static string? Problem(JsonNode s, string? key, HashSet<string> items, HashSet<string> seen) =>
        key is null || !HoldoutFiles.IsKey(key) ? "a scenario id must be letters, digits, '.', '_' or '-'"
        : !seen.Add(key) ? $"scenario {key} is repeated"
        : LineJson.Str(s, "item") is not { } item || !items.Contains(item) ? $"scenario {key} names no visible acceptance item of the phase"
        : !LineJson.Strings(s, "steps").Any() || string.IsNullOrWhiteSpace(LineJson.Str(s, "expected")) || string.IsNullOrWhiteSpace(LineJson.Str(s, "kind")) ? $"scenario {key} needs a kind, steps and an expected result"
        : null;

    private static Prepared Prepare(string phase, string key, IEnumerable<JsonNode> scenarios)
    {
        var author = scenarios.Where(static s => !string.Equals(LineJson.Str(s, "kind"), HoldoutScenario.CanaryKind, StringComparison.Ordinal)).ToList();
        var list = new List<JsonNode?>([.. author.Select(static s => new JsonObject
        {
            ["id"] = LineJson.Str(s, "id"),
            ["item"] = LineJson.Str(s, "item"),
            ["kind"] = LineJson.Str(s, "kind"),
            ["steps"] = LineJson.ToArray(LineJson.Strings(s, "steps")),
            ["expected"] = LineJson.Str(s, "expected"),
        })])
        {
            new JsonObject
            {
                ["id"] = key + "-canary",
                ["item"] = null,
                ["kind"] = HoldoutScenario.CanaryKind,
                ["steps"] = new JsonArray(),
                ["expected"] = "never",
                ["phrase"] = "canary-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(12)),
            },
        };
        var set = new JsonObject { ["schemaVersion"] = 1, ["phase"] = key, ["scenarios"] = new JsonArray([.. list]) };
        return new Prepared(phase, key, set.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), author.Count);
    }

    private sealed record Prepared(string Phase, string Key, string Text, int Count);
}
