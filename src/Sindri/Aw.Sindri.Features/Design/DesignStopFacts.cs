using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>
/// A workflow.plan instance's facts as the files the stop view reads (line spec LN-API2): fact:plan is tasks.json (PKT's task plan), fact:holdoutSets holdout.json and fact:freezeRefused
/// freezeRefused.json; fact:design is an object with one member per design packet file, named as packet.json links it (spec, sections, coverage, decisions, acceptance, signals,
/// contracts, lensRounds, style, escapes, runs): '.json' files hold their document, lensRounds an array of its lines and spec the markdown.
/// </summary>
internal static class DesignStopFacts
{
    private static readonly (string Member, string File)[] _designFiles =
    [
        ("spec", "spec.md"), ("sections", "sections.json"), ("coverage", "coverage.json"), ("decisions", "decisions.json"), ("acceptance", "acceptance.json"), ("signals", "signals.json"),
        ("contracts", "contracts.json"), ("lensRounds", "lens-rounds.jsonl"), ("style", "style.json"), ("escapes", "escapes.json"), ("runs", "runs.json"),
    ];

    /// <summary>The instance's facts (JSON text by key without the 'fact:' prefix) as files.</summary>
    /// <param name="facts">The facts.</param>
    internal static Dictionary<string, string> Files(IReadOnlyDictionary<string, string> facts)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (fact, file) in new[] { ("plan", "tasks.json"), ("holdoutSets", "holdout.json"), ("freezeRefused", "freezeRefused.json") })
        {
            if (facts.TryGetValue(fact, out var text))
            {
                files[file] = text;
            }
        }

        if (facts.TryGetValue("design", out var design))
        {
            foreach (var (name, content) in DesignFiles(design))
            {
                files["design/" + name] = content;
            }
        }

        return files;
    }

    private static Dictionary<string, string> DesignFiles(string design)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            if (JsonNode.Parse(design) is not JsonObject parts)
            {
                return files;
            }

            foreach (var (member, file) in _designFiles)
            {
                if (parts[member] is { } part)
                {
                    files[file] = Content(part, file);
                }
            }
        }
        catch (JsonException)
        {
            // boundary: a fact is text a Step or a person wrote; one that is not JSON holds no design files
        }

        return files;
    }

    private static string Content(JsonNode part, string file)
    {
        if (file.EndsWith(".md", StringComparison.Ordinal))
        {
            return part is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;
        }

        if (file.EndsWith(".jsonl", StringComparison.Ordinal))
        {
            return new StringBuilder().AppendJoin('\n', LineJson.Items(part).Select(static l => l.ToJsonString())).ToString();
        }

        return part.ToJsonString();
    }
}
