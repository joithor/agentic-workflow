using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// The design packet's files (line spec §3.2 row 2, §4.4) and the 'fact:design' they come from. Each file is the fact's field wrapped with 'schemaVersion':
/// spec.md the text of design.spec; sections.json design.sections; coverage.json design.coverage; decisions.json design.decisions, deferred and resolutions;
/// signals.json design.signals; contracts.json design.contracts with the scope's baseSha; acceptance.json design.acceptance (items) with its parent, the scope's
/// acceptance.json digest; escapes.json design.escapes; lens-rounds.jsonl 'fact:lensRounds', a round a line; style.json 'fact:packs'; runs.json design.runs and the
/// owner's accepted risks. <see cref="FromFiles"/> reads them back into a fact (the plan station's intake).
/// </summary>
internal static class DesignPacket
{
    private const string Kind = "design";

    /// <summary>The files, in the order they are written; packet.json and index.json last.</summary>
    /// <param name="scope">The 'fact:scope' object.</param>
    /// <param name="packs">The 'fact:packs' tree.</param>
    /// <param name="design">The 'fact:design' tree.</param>
    /// <param name="lens">The lens rounds and the accepted risks.</param>
    /// <param name="repo">The repo slug ('owner/name').</param>
    /// <returns>The files, packet.json and index.json last; why the design cannot be a packet.</returns>
    internal static Result<IReadOnlyList<PacketText>> Assemble(JsonNode scope, JsonNode? packs, JsonNode design, (JsonNode? Rounds, JsonNode? Accepted) lens, string repo)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(design);
        var spec = LineJson.Str(design, "spec");
        var baseSha = LineJson.Str(scope, "baseSha");
        if (string.IsNullOrWhiteSpace(spec) || string.IsNullOrWhiteSpace(baseSha))
        {
            return SindriErrors.InvalidInput.With(string.IsNullOrWhiteSpace(spec)
                ? "fact:design has no spec text (fix: the draft Step writes the spec.md text as design.spec)"
                : "fact:scope has no baseSha (fix: the scope packet's scope-map.json names the commit it was scoped at)");
        }

        return Files(scope, packs, design, lens, (repo, baseSha));
    }

    /// <summary>The design fact a packet's files make: the inverse of <see cref="Assemble"/>, with 'lensRounds' (the rounds), 'accepted' (the owner's accepted risks), 'acceptanceHash' (the digest of acceptance.json, which the plan's acceptance chain extends) and 'packet' ({hash, dir}) added.</summary>
    /// <param name="texts">The files' text by name.</param>
    /// <param name="packet">The packet.</param>
    /// <returns>The design fact; why a file cannot be read.</returns>
    internal static Result<JsonObject> FromFiles(IReadOnlyDictionary<string, string> texts, PacketFact packet)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(packet);
        var parsed = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var name in new[] { "sections.json", "coverage.json", "decisions.json", "acceptance.json", "signals.json", "contracts.json", "escapes.json", "runs.json" })
        {
            if (!texts.TryGetValue(name, out var text) || Parse(text) is not JsonObject obj)
            {
                return SindriErrors.InvalidInput.With($"the design packet's {name} is missing or not a JSON object (fix: re-run the design station; a packet is never edited)");
            }

            parsed[name] = obj;
        }

        var coverage = (JsonObject)parsed["coverage.json"]!.DeepClone(); // why: the loop above parsed it as an object
        _ = coverage.Remove("schemaVersion");
        var acceptance = (JsonObject)parsed["acceptance.json"]!.DeepClone(); // why: as above
        _ = acceptance.Remove("schemaVersion");
        return new JsonObject
        {
            ["spec"] = texts.GetValueOrDefault("spec.md", string.Empty),
            ["sections"] = Array(parsed["sections.json"], "sections"),
            ["coverage"] = coverage,
            ["decisions"] = Array(parsed["decisions.json"], "decisions"),
            ["deferred"] = Array(parsed["decisions.json"], "deferred"),
            ["resolutions"] = Array(parsed["decisions.json"], "resolutions"),
            ["signals"] = Array(parsed["signals.json"], "signals"),
            ["contracts"] = Array(parsed["contracts.json"], "contracts"),
            ["acceptance"] = acceptance,
            ["acceptanceHash"] = LinePackets.Sha256(texts["acceptance.json"]),
            ["escapes"] = Array(parsed["escapes.json"], "escapes"),
            ["runs"] = Array(parsed["runs.json"], "runs"),
            ["accepted"] = Array(parsed["runs.json"], "accepted"),
            ["lensRounds"] = Rounds(texts),
            ["packet"] = packet.ToJson(),
        };
    }

    /// <summary>The packet's file names, for a station that reads them.</summary>
    internal static IReadOnlyList<string> FileNames { get; } = ["spec.md", "sections.json", "coverage.json", "decisions.json", "acceptance.json", "signals.json", "contracts.json", "lens-rounds.jsonl", "style.json", "escapes.json", "runs.json"];

    private static List<PacketText> Files(JsonNode scope, JsonNode? packs, JsonNode design, (JsonNode? Rounds, JsonNode? Accepted) lens, (string Repo, string BaseSha) origin)
    {
        var acceptance = LinePackets.Json(LinePackets.Doc(("parent", scope["acceptanceHash"]), ("items", LineJson.Obj(design, "acceptance") is { } own ? own["items"] : scope["acceptance"]?["items"])));
        List<PacketText> files =
        [
            new("spec.md", LineJson.Str(design, "spec") ?? string.Empty),
            new("sections.json", LinePackets.Json(LinePackets.Doc(("sections", Array(design, "sections"))))),
            new("coverage.json", LinePackets.Json(Coverage(design))),
            new("decisions.json", LinePackets.Json(LinePackets.Doc(("decisions", Array(design, "decisions")), ("deferred", Array(design, "deferred")), ("resolutions", Array(design, "resolutions"))))),
            new("acceptance.json", acceptance),
            new("signals.json", LinePackets.Json(LinePackets.Doc(("signals", Array(design, "signals"))))),
            new("contracts.json", LinePackets.Json(LinePackets.Doc(("baseSha", origin.BaseSha), ("contracts", Array(design, "contracts"))))),
            new("lens-rounds.jsonl", string.Concat(LineJson.Items(lens.Rounds).Select(static r => LineJson.Write(r) + "\n"))),
            new("style.json", LinePackets.Json(Style(packs))),
            new("escapes.json", LinePackets.Json(LinePackets.Doc(("escapes", Escapes(design))))),
            new("runs.json", LinePackets.Json(LinePackets.Doc(("runs", Array(design, "runs")), ("accepted", lens.Accepted is JsonArray a ? a : [])))),
            new("index.json", LinePackets.Json(Index(scope, acceptance))),
            new("packet.json", LinePackets.Json(Packet(scope, origin.Repo, origin.BaseSha))),
        ];
        return files;
    }

    private static JsonArray Rounds(IReadOnlyDictionary<string, string> texts)
    {
        JsonArray rounds = [];
        foreach (var line in texts.GetValueOrDefault("lens-rounds.jsonl", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            LineJson.Append(rounds, Parse(line) ?? new JsonObject());
        }

        return rounds;
    }

    private static JsonArray Array(JsonNode? owner, string name) => LineJson.Arr(owner, name) is { } array ? (JsonArray)array.DeepClone() : [];

    private static JsonObject Coverage(JsonNode design)
    {
        var coverage = LineJson.Obj(design, "coverage") is { } own ? (JsonObject)own.DeepClone() : [];
        _ = coverage.Remove("schemaVersion");
        var doc = LinePackets.Doc();
        foreach (var (name, value) in coverage)
        {
            doc[name] = value?.DeepClone();
        }

        return doc;
    }

    private static JsonObject Style(JsonNode? packs)
    {
        var doc = LinePackets.Doc();
        foreach (var name in new[] { "style", "lenses" })
        {
            if (packs is JsonObject obj && obj[name] is { } pack)
            {
                doc[name] = pack.DeepClone();
            }
        }

        return doc;
    }

    private static JsonArray Escapes(JsonNode design)
    {
        var escapes = new JsonArray();
        foreach (var entry in LineJson.Items(design, "escapes").OfType<JsonObject>().Select(static e => (JsonObject)e.DeepClone()))
        {
            entry["from"] ??= "scoping";
            entry["station"] ??= "design";
            LineJson.Append(escapes, entry);
        }

        return escapes;
    }

    private static JsonObject Index(JsonNode scope, string acceptance) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = Kind,
        ["parents"] = new JsonArray(LineJson.Str(scope, "hash")),
        ["acceptance"] = new JsonObject { ["self"] = LinePackets.Sha256(acceptance), ["extends"] = scope["acceptanceHash"]?.DeepClone() },
    };

    private static JsonObject Packet(JsonNode scope, string repo, string baseSha) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = Kind,
        ["id"] = "design-" + LinePackets.Slug(LineJson.Str(scope, "change") ?? "change"),
        ["repo"] = repo,
        ["baseSha"] = baseSha,
        ["status"] = "draft",
        ["hash"] = null,
        ["links"] = new JsonObject
        {
            ["spec"] = "spec.md",
            ["sections"] = "sections.json",
            ["coverage"] = "coverage.json",
            ["decisions"] = "decisions.json",
            ["acceptance"] = "acceptance.json",
            ["signals"] = "signals.json",
            ["contracts"] = "contracts.json",
            ["lensRounds"] = "lens-rounds.jsonl",
            ["style"] = "style.json",
            ["escapes"] = "escapes.json",
            ["runs"] = "runs.json",
            ["index"] = "index.json",
        },
    };

    private static JsonNode? Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;   // boundary: a packet file is text a model wrote
        }
    }
}
