using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

public static partial class ProofPack
{
    /// <summary>The names of the pack's files, in the order the packet's links list them.</summary>
    public static IReadOnlyList<string> FileNames { get; } = ["packet.json", "index.json", "proof.json", "red.json", "before-after.json", "holdout.json", "not-proven.md", "acceptance.json"];

    /// <summary>Every file of the pack as text, by name; the pack has no 'app-run/' or 'evidence/' files until the evidence Step reads the children's packs.</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="report">What the rules found: the rules that did not run are recorded in proof.json.</param>
    /// <param name="origin">The phase packet the pack proves.</param>
    /// <returns>File name to text.</returns>
    public static IReadOnlyDictionary<string, string> Files(ProofFacts facts, ProofReport report, ProofPackOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(origin);
        var acceptance = Acceptance(facts);
        acceptance["parent"] = origin.AcceptanceHash;
        var acceptanceText = acceptance.ToJsonString();
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["packet.json"] = Packet(origin).ToJsonString(),
            ["index.json"] = Index(origin, acceptanceText).ToJsonString(),
            ["proof.json"] = Proof(facts, report).ToJsonString(),
            ["red.json"] = Red(facts).ToJsonString(),
            ["before-after.json"] = BeforeAfter(facts).ToJsonString(),
            ["holdout.json"] = Holdout(facts).ToJsonString(),
            ["not-proven.md"] = NotProven(facts),
            ["acceptance.json"] = acceptanceText,
        };
    }

    /// <summary>'sha256:' and the hex digest of a text's UTF-8 bytes, the way a packet file's hash is written.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The hash.</returns>
    public static string Sha256(string text) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static JsonObject Packet(ProofPackOrigin origin) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = "proof",
        ["id"] = origin.Slug,
        ["repo"] = origin.Repo,
        ["baseSha"] = origin.BaseSha,
        ["status"] = "draft",
        ["hash"] = null,
        ["links"] = new JsonObject
        {
            ["proof"] = "proof.json",
            ["red"] = "red.json",
            ["beforeAfter"] = "before-after.json",
            ["holdout"] = "holdout.json",
            ["notProven"] = "not-proven.md",
            ["acceptance"] = "acceptance.json",
            ["index"] = "index.json",
            ["appRun"] = new JsonArray(),
            ["evidence"] = new JsonArray(),
        },
    };

    private static JsonObject Index(ProofPackOrigin origin, string acceptanceText) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = "proof",
        ["parents"] = new JsonArray(origin.PacketHash),
        ["acceptance"] = new JsonObject { ["self"] = Sha256(acceptanceText), ["extends"] = origin.AcceptanceHash },
    };

    private static JsonObject Proof(ProofFacts facts, ProofReport report)
    {
        var items = new JsonArray();
        foreach (var id in ProofChecks.LiveItems(facts.Acceptance))
        {
            var artifacts = new JsonArray();
            foreach (var artifact in (facts.Proof?["items"] as JsonArray ?? []).OfType<JsonObject>().Where(i => (string?)i["id"] == id).SelectMany(static i => (i["artifacts"] as JsonArray ?? []).OfType<JsonObject>()))
            {
                var copy = (JsonObject)artifact.DeepClone();
                copy["superseded"] = (bool?)artifact["superseded"] == true || (string?)artifact["commit"] != facts.Head;
                artifacts.Add((JsonNode)copy);
            }

            items.Add((JsonNode)new JsonObject { ["id"] = id, ["passing"] = Backing(facts, id).Count > 0, ["artifacts"] = artifacts });
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["head"] = facts.Head,
            ["items"] = items,
            ["notRun"] = new JsonArray([.. report.NotRun.Select(static n => (JsonNode)JsonValue.Create(n))]),
        };
    }

    private static JsonObject Red(ProofFacts facts)
    {
        var file = facts.Red as JsonObject is { } red ? (JsonObject)red.DeepClone() : [];
        file["schemaVersion"] = 1;
        return file;
    }

    private static JsonObject BeforeAfter(ProofFacts facts) => new()
    {
        ["schemaVersion"] = 1,
        ["commit"] = (string?)facts.BeforeAfter?["commit"] ?? facts.Head,
        ["results"] = facts.BeforeAfter?["results"]?.DeepClone() ?? new JsonArray(),
    };

    private static JsonObject Holdout(ProofFacts facts)
    {
        var results = new JsonArray();
        foreach (var result in ProofChecks.HiddenResults(facts.Holdout))
        {
            var kept = Project(result, "scenario", "item", "verdict");
            kept["spent"] = (bool?)result["spent"] == true;
            results.Add((JsonNode)kept);
        }

        return new JsonObject { ["schemaVersion"] = 1, ["commit"] = (string?)facts.Holdout?["commit"] ?? facts.Head, ["results"] = results };
    }

    private static string NotProven(ProofFacts facts)
    {
        var lines = new List<string>();
        foreach (var id in ProofChecks.LiveItems(facts.Acceptance))
        {
            foreach (var signal in PhaseSignals.Of(facts.Signals, id).Where(s => !SignalProven(facts, id, (string?)s["id"])))
            {
                lines.Add($"- {id} / {(string?)signal["id"]} ({(string?)signal["kind"]}): {(string?)signal["statement"]}");
            }
        }

        return lines.Count == 0 ? "nothing skipped\n" : "# Not proven\n\n" + string.Join('\n', lines) + "\n";
    }

    private static bool SignalProven(ProofFacts facts, string item, string? signal) =>
        Backing(facts, item).Exists(a => (string?)a["signal"] == signal);
}
