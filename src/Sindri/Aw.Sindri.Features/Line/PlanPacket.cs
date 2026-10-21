using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// The plan packet's files (line spec §3.2 row 3, §6): tasks.json is PKT's task plan made from 'fact:plan' (phase to task: title, workstream, deps, size, writeScope from
/// the phase's writeScope or allowedFiles, readSeeds, acceptance, budget, and rules 'kind:&lt;kind&gt;' plus a 'test-change:&lt;path&gt;:&lt;reason&gt;' per testChange);
/// graph.json is <see cref="PlanGraph"/>; exec-review.json the plan's execReview; holdout.json the sets by hash and count, never text; answers.jsonl the answer log.
/// </summary>
internal static class PlanPacket
{
    /// <summary>The id of a plan packet and its phases' ids are made from this slug.</summary>
    /// <param name="scope">The 'fact:scope' object.</param>
    /// <returns>The slug of the change.</returns>
    internal static string Slug(JsonNode scope) => LinePackets.Slug(LineJson.Str(scope, "change") ?? "change");

    /// <summary>The files, in the order they are written; index.json and packet.json last.</summary>
    /// <param name="scope">The 'fact:scope' object.</param>
    /// <param name="design">The 'fact:design' object, as the plan station's intake loaded it (its 'packet' and 'acceptanceHash').</param>
    /// <param name="plan">The 'fact:plan' tree.</param>
    /// <param name="sets">The holdout sets and the answer log.</param>
    /// <param name="repo">The repo slug ('owner/name').</param>
    /// <returns>The files; why the plan cannot be a packet.</returns>
    internal static Result<IReadOnlyList<PacketText>> Assemble(JsonNode scope, JsonNode design, JsonNode plan, (JsonNode? Holdout, JsonNode? AnswerLog) sets, string repo)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(design);
        ArgumentNullException.ThrowIfNull(plan);
        var baseSha = LineJson.Str(scope, "baseSha");
        var designHash = LineJson.Str(LineJson.Obj(design, "packet"), "hash");
        if (string.IsNullOrWhiteSpace(baseSha) || string.IsNullOrWhiteSpace(designHash))
        {
            return SindriErrors.InvalidInput.With("the plan packet needs fact:scope's baseSha and the design packet's hash (fix: run the plan station's intake)");
        }

        return new List<PacketText>
        {
            new("tasks.json", LinePackets.Json(Tasks(plan))),
            new("graph.json", LinePackets.Json(PlanGraph.Derive(plan))),
            new("exec-review.json", LinePackets.Json(LinePackets.Doc(("reviews", LineJson.Arr(plan, "execReview") ?? [])))),
            new("holdout.json", LinePackets.Json(Holdout(plan, sets.Holdout))),
            new("answers.jsonl", string.Concat(LineJson.Items(sets.AnswerLog).Select(static a => LineJson.Write(a) + "\n"))),
            new("index.json", LinePackets.Json(new JsonObject
            {
                ["schemaVersion"] = 1,
                ["kind"] = "plan",
                ["parents"] = new JsonArray(designHash, LineJson.Str(scope, "hash")),
                ["acceptance"] = new JsonObject { ["self"] = null, ["extends"] = design["acceptanceHash"]?.DeepClone() },
            })),
            new("packet.json", LinePackets.Json(new JsonObject
            {
                ["schemaVersion"] = 1,
                ["kind"] = "plan",
                ["id"] = "plan-" + Slug(scope),
                ["repo"] = repo,
                ["baseSha"] = baseSha,
                ["status"] = "draft",
                ["hash"] = null,
                ["links"] = new JsonObject { ["tasks"] = "tasks.json", ["graph"] = "graph.json", ["execReview"] = "exec-review.json", ["holdout"] = "holdout.json", ["answers"] = "answers.jsonl", ["index"] = "index.json" },
            })),
        };
    }

    /// <summary>The testChange entries of a phase: its 'testChange' array of {path, reason}, or the 'test-change:&lt;path&gt;:&lt;reason&gt;' entries of its rules.</summary>
    /// <param name="phase">The phase.</param>
    /// <returns>The entries.</returns>
    internal static IReadOnlyList<JsonNode> TestChanges(JsonNode phase)
    {
        if (LineJson.Arr(phase, "testChange") is { } own)
        {
            return [.. own.OfType<JsonNode>().Select(static n => n.DeepClone())];
        }

        var changes = new List<JsonNode>();
        foreach (var rule in LineJson.Strings(phase, "rules").Where(static r => r.StartsWith("test-change:", StringComparison.Ordinal)))
        {
            var rest = rule["test-change:".Length..];
            var split = rest.IndexOf(':', StringComparison.Ordinal);
            changes.Add(new JsonObject { ["path"] = split < 0 ? rest : rest[..split], ["reason"] = split < 0 ? string.Empty : rest[(split + 1)..] });
        }

        return changes;
    }

    private static JsonObject Tasks(JsonNode plan)
    {
        var workstreams = new JsonArray();
        var tasks = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var phase in LineJson.Items(plan, "phases"))
        {
            var workstream = LineJson.Str(phase, "workstream") ?? "main";
            if (seen.Add(workstream))
            {
                LineJson.Append(workstreams, new JsonObject { ["id"] = workstream, ["title"] = LineJson.Str(plan, "title") ?? workstream });
            }

            LineJson.Append(tasks, Task(phase, workstream));
        }

        return LinePackets.Doc(("workstreams", workstreams), ("tasks", tasks));
    }

    private static JsonObject Task(JsonNode phase, string workstream)
    {
        var rules = new JsonArray();
        LineJson.Append(rules, "kind:" + (LineJson.Str(phase, "kind") ?? "feature"));
        foreach (var change in TestChanges(phase))
        {
            LineJson.Append(rules, $"test-change:{LineJson.Str(change, "path")}:{LineJson.Str(change, "reason")}");
        }

        return new JsonObject
        {
            ["id"] = PlanContext.Id(phase),
            ["workstream"] = workstream,
            ["title"] = LineJson.Str(phase, "title") ?? PlanContext.Id(phase),
            ["deps"] = Clone(LineJson.Arr(phase, "deps")),
            ["size"] = LineJson.Str(phase, "size"),
            ["writeScope"] = Clone(LineJson.Arr(phase, "writeScope") ?? LineJson.Arr(phase, "allowedFiles")),
            ["readSeeds"] = Clone(LineJson.Arr(phase, "readSeeds")),
            ["acceptance"] = Clone(LineJson.Arr(phase, "acceptance")),
            ["rules"] = rules,
            ["budget"] = LineJson.Obj(phase, "budget") is { } budget ? budget.DeepClone() : new JsonObject(),
        };
    }

    private static JsonObject Holdout(JsonNode plan, JsonNode? sets)
    {
        var phases = new JsonArray();
        foreach (var set in LineJson.Items(sets))
        {
            LineJson.Append(phases, new JsonObject
            {
                ["id"] = LineJson.Str(set, "phase"),
                ["setHash"] = LineJson.Str(set, "hash"),
                ["count"] = LineJson.Int(set, "count") ?? 0,
                ["canaries"] = LineJson.Bool(set, "canary") == true ? 1 : 0,
            });
        }

        return LinePackets.Doc(("minPerPhase", LineJson.Int(plan, "holdoutMin") ?? 2), ("phases", phases));
    }

    private static JsonArray Clone(JsonArray? array) => array is null ? [] : (JsonArray)array.DeepClone();
}
