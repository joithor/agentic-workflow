using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

/// <summary>
/// The proof pack (line spec §3.2 row 5, §7.3): the files <c language="text">step.prove.evidence</c> writes into the run area and machine-freezes, as pure functions of <see cref="ProofFacts"/>.
/// The pack's acceptance.json is the phase's with only statuses flipped (LC-2): an item is 'passing' when a passing artifact of the head backs it, and then carries that evidence.
/// </summary>
public static partial class ProofPack
{
    /// <summary>The acceptance file the pack carries: the phase's items, each flipped to 'passing' with its evidence when a passing artifact of the head backs it.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The file's JSON (its parent is still the phase's).</returns>
    public static JsonNode Acceptance(ProofFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var file = facts.Acceptance.DeepClone();
        foreach (var item in (file["items"] as JsonArray ?? []).OfType<JsonObject>().Where(static i => (string?)i["status"] != "superseded"))
        {
            var backing = Backing(facts, (string?)item["id"] ?? string.Empty);
            if (backing.Count > 0)
            {
                item["status"] = "passing";
                item["evidence"] = new JsonArray([.. backing.Select(Reference)]);
            }
        }

        return file;
    }

    /// <summary>PRV-ACC-002 (LC-ACC-001): the pack's acceptance only flips statuses, each flip with evidence.</summary>
    /// <param name="before">The phase's acceptance file.</param>
    /// <param name="after">The pack's.</param>
    /// <returns>One finding per item that changed more than its status, or flipped without evidence.</returns>
    public static IReadOnlyList<ProofFinding> AcceptanceFlips(JsonNode before, JsonNode after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var was = (before["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var now = (after["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (was.Count != now.Count)
        {
            return [new ProofFinding("PRV-ACC-002", Item: null, string.Create(CultureInfo.InvariantCulture, $"the pack's acceptance has {now.Count} items, the phase's has {was.Count}"), "the pack only flips statuses; it never adds or drops an item")];
        }

        return [.. was.Zip(now).Select(static p => Flip((string?)p.First["id"], p.First, p.Second)).OfType<ProofFinding>()];
    }

    private static ProofFinding? Flip(string? id, JsonObject was, JsonObject now)
    {
        var changed = !Same(was, now);
        var unbacked = (string?)now["status"] == "passing" && (string?)was["status"] != "passing" && (now["evidence"] as JsonArray ?? []).Count == 0;
        return changed || unbacked
            ? new ProofFinding("PRV-ACC-002", id, $"{id}: the pack's acceptance {(changed ? "changed more than the item's status and evidence" : "flips the item to passing without evidence")}", "only flip an item's status, and give each flip its evidence")
            : null;
    }

    private static bool Same(JsonObject was, JsonObject now) =>
        Plain(was) == Plain(now);

    private static string Plain(JsonObject item)
    {
        var copy = (JsonObject)item.DeepClone();
        _ = copy.Remove("status");
        _ = copy.Remove("evidence");
        return copy.ToJsonString();
    }

    private static List<JsonObject> Backing(ProofFacts facts, string id) =>
        [.. (facts.Proof?["items"] as JsonArray ?? []).OfType<JsonObject>().Where(i => (string?)i["id"] == id)
            .SelectMany(static i => (i["artifacts"] as JsonArray ?? []).OfType<JsonObject>())
            .Where(a => (bool?)a["passing"] == true && (string?)a["commit"] == facts.Head && (bool?)a["superseded"] != true)];

    private static JsonObject Reference(JsonObject artifact) => Project(artifact, "kind", "path", "sha256", "commit", "at");

    /// <summary>A new object holding only the named fields of <paramref name="source"/> (an absent one as an empty text).</summary>
    private static JsonObject Project(JsonObject source, params string[] keys)
    {
        var projected = new JsonObject();
        foreach (var key in keys)
        {
            projected[key] = source[key]?.DeepClone() ?? JsonValue.Create(string.Empty);
        }

        return projected;
    }
}
