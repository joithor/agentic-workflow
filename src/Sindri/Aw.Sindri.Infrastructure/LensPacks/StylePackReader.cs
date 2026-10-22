using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary>Reads a style pack from its files (line spec §5.1): <c language="text">pack.json</c> with its sections and rules, and the template, voice and example files it names.</summary>
internal static class StylePackReader
{
    /// <summary>The pack the files hold.</summary>
    /// <param name="id">The id the pack was asked for; the file must say the same.</param>
    /// <param name="files">Each file's path inside the pack and its text.</param>
    /// <returns>The pack; an error naming what is wrong with the file.</returns>
    internal static Result<StylePack> Read(string id, IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(files);
        var header = Header(id, files);
        if (header.IsError)
        {
            return header.Error;
        }

        var (packId, pack) = header.Value;
        var sections = Sections(id, pack["sections"]);
        if (sections.IsError)
        {
            return sections.Error;
        }

        var rules = Rules(id, pack["rules"] as JsonObject, files);
        if (rules.IsError)
        {
            return rules.Error;
        }

        return Named(pack, files) is { } missing
            ? Bad(id, missing)
            : new StylePack(packId, LensPackReader.Hash(files), sections.Value, rules.Value);
    }

    private static Result<(LensPackId Id, JsonObject Pack)> Header(string id, IReadOnlyDictionary<string, string> files)
    {
        if (!files.TryGetValue("pack.json", out var text))
        {
            return Bad(id, "has no pack.json");
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(text);
        }
        catch (JsonException e)
        {
            return Bad(id, $"has a pack.json that is not JSON: {e.Message}");
        }

        if (root is not JsonObject pack || Text(pack, "kind") != "style" || Text(pack, "id") != id || !LensPackId.TryFrom(id, out var packId))
        {
            return Bad(id, $"has a pack.json that is not a style pack with the id '{id}' (a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters)");
        }

        return string.IsNullOrWhiteSpace(Text(pack, "version")) || string.IsNullOrWhiteSpace(Text(pack, "title"))
            ? Bad(id, "has a pack.json without a version or a title")
            : (packId, pack);
    }

    private static Result<IReadOnlyList<StyleSection>> Sections(string id, JsonNode? node)
    {
        if (node is not JsonArray { Count: > 0 } entries)
        {
            return Bad(id, "has no section");
        }

        var sections = new List<StyleSection>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var section = Section(id, entry as JsonObject);
            if (section.IsError)
            {
                return section.Error;
            }

            if (!seen.Add(section.Value.Heading))
            {
                return Bad(id, $"names the heading '{section.Value.Heading}' twice");
            }

            sections.Add(section.Value);
        }

        return sections;
    }

    private static Result<StyleSection> Section(string id, JsonObject? entry)
    {
        var heading = Text(entry, "heading");
        if (string.IsNullOrWhiteSpace(heading) || string.IsNullOrWhiteSpace(Text(entry, "id")))
        {
            return Bad(id, "has a section without an id or a heading");
        }

        if (entry?["required"] is not JsonValue flag || !flag.TryGetValue<bool>(out var required))
        {
            return Bad(id, $"has section '{heading}' whose required flag is not true or false");
        }

        var limit = entry["maxWords"];
        if (limit is null)
        {
            return new StyleSection(heading, required, MaxWords: null);
        }

        return Positive(limit) is { } words
            ? new StyleSection(heading, required, words)
            : Bad(id, $"has section '{heading}' whose maxWords is not a whole number above 0");
    }

    private static Result<StyleRules> Rules(string id, JsonObject? rules, IReadOnlyDictionary<string, string> files)
    {
        if (rules is null)
        {
            return Bad(id, "has no rules");
        }

        if (Text(rules, "tables") is not ("prefer" or "allow"))
        {
            return Bad(id, "says rules.tables something other than prefer or allow");
        }

        if (Text(rules, "voice") is not { } voice || !files.ContainsKey(voice))
        {
            return Bad(id, $"names the voice file '{Text(rules, "voice")}' of its rules, which is not in the pack");
        }

        var forbidden = Forbidden(rules["forbidden"]);
        if (forbidden is null)
        {
            return Bad(id, "has a rules.forbidden that is not a list of phrases");
        }

        var cap = rules["maxLines"];
        return cap is not null && Positive(cap) is null
            ? Bad(id, "has a rules.maxLines that is not a whole number above 0")
            : new StyleRules(forbidden, cap is null ? null : Positive(cap));
    }

    // The files the pack names besides the voice: its template and each example.
    private static string? Named(JsonObject pack, IReadOnlyDictionary<string, string> files)
    {
        if (Text(pack, "template") is not { } template || !files.ContainsKey(template))
        {
            return $"names the template '{Text(pack, "template")}', which is not in the pack";
        }

        if (pack["examples"] is not JsonArray examples)
        {
            return "has no examples list";
        }

        foreach (var example in examples)
        {
            var path = example is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            if (path is null || !files.ContainsKey(path))
            {
                return $"names the example '{path}', which is not in the pack";
            }
        }

        return null;
    }

    private static List<string>? Forbidden(JsonNode? node) => node switch
    {
        null => [],
        JsonArray array when array.All(static n => n is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)) => [.. array.Select(static n => n!.GetValue<string>())],   // why: the guard checked every element is a string
        _ => null,
    };

    private static int? Positive(JsonNode? node) => node is JsonValue v && v.TryGetValue<int>(out var n) && n > 0 ? n : null;

    private static string? Text(JsonObject? obj, string name) => obj?[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static Error Bad(string id, string problem) => SindriErrors.InvalidInput.With($"the style pack '{id}' {problem}");
}
