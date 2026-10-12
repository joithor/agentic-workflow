using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The YAML recipe reader (spec §3.1): YamlDotNet's parser only (AOT-safe), the YAML 1.2 core schema, the refused
/// features as RCP-SCHEMA-003, short forms normalized to the long form, every unknown field, missing field and wrong
/// type reported at its node (RCP-SCHEMA-001/002, RCP-DUR-001), then typed binding with unmapped members disallowed
/// and the hash over the long form. The file's text is all it reads; the caller does the I/O.
/// </summary>
internal sealed class YamlRecipeReader(JsonSerializerOptions options) : IRecipeReader
{
    /// <summary>The largest recipe file read, in UTF-8 bytes (256 KiB).</summary>
    internal const int MaxBytes = 256 * 1024;

    public RecipeReading Read(string text, string file)
    {
        ArgumentNullException.ThrowIfNull(text);
        var findings = new ReaderFindings(file);
        var size = Encoding.UTF8.GetByteCount(text);
        if (size > MaxBytes)
        {
            findings.At("RCP-SCHEMA-003", string.Empty, SourcePosition.Start,
                string.Create(CultureInfo.InvariantCulture, $"the file is {size} bytes, over the 256 KiB limit"), "split the recipe, or move long texts out of it");
            return new RecipeReading(Document: null, findings.All);
        }

        var root = YamlTree.Read(text, findings);
        if (root is JsonObject recipe)
        {
            RecipeNormalizer.Normalize(recipe, findings);
            new RecipeShape(options, findings).Check(recipe, typeof(Recipe), string.Empty, nullable: false);
        }
        else if (!findings.Any)
        {
            findings.Add("RCP-SCHEMA-002", string.Empty, "the recipe isn't a mapping",
                "write the recipe as a YAML mapping with schema, id, version, initial, states and transitions");
        }

        if (!findings.Any)
        {
            return Bind((JsonObject)root!, file, findings);   // why: no finding means the root was a mapping
        }

        return root is JsonObject partial ? new RecipeReading(Document: null, [.. findings.All, .. SchemaPastRefusals(partial, file, findings)])
            : new RecipeReading(Document: null, findings.All);
    }

    /// <summary>
    /// The schema rules' findings on what is left of a recipe the reader refused parts of (spec §3.1: every schema error
    /// is reported, not just the first). Each refused field or map entry is left out; when the rest still binds, the
    /// schema rules run on it, and their findings on refused nodes, or already reported, are dropped. Nothing is added
    /// after refused YAML (RCP-SCHEMA-003), a refused list item (leaving it out would shift the items after it) or a
    /// recipe that doesn't bind without its refused fields (a required one). It is never hashed or validated further.
    /// </summary>
    private IEnumerable<RecipeFinding> SchemaPastRefusals(JsonObject root, string file, ReaderFindings findings)
    {
        var refused = findings.Refused.ToList();
        if (findings.All.Any(static f => f.Rule == "RCP-SCHEMA-003") || refused.Exists(static p => p.Length == 0 || p.EndsWith(']')))
        {
            return [];
        }

        var copy = (JsonObject)root.DeepClone();
        foreach (var path in refused)
        {
            LeaveOut(copy, path);
        }

        Recipe? recipe;
        try
        {
            recipe = (Recipe?)JsonSerializer.Deserialize(copy, options.GetTypeInfo(typeof(Recipe)));
        }
        catch (JsonException)
        {
            return [];
        }

        var reported = findings.All.Select(static f => (f.Rule, f.Path)).ToHashSet();
        var document = new RecipeDocument(recipe!, RecipeHashing.Of(recipe!, options), new RecipeSource(file, findings.Positions));   // why: the root is a mapping, never JSON null
        return [.. RecipeValidator.CheckSchema(document).Where(f => !findings.IsRefused(f.Path) && !reported.Contains((f.Rule, f.Path)))];
    }

    /// <summary>Leaves out the field or map entry at <paramref name="path"/> (never a list item); a path already gone is skipped.</summary>
    private static void LeaveOut(JsonObject root, string path)
    {
        var cut = path.LastIndexOf('.');
        if ((cut < 0 ? root : Find(root, path[..cut])) is JsonObject parent)
        {
            _ = parent.Remove(cut < 0 ? path : path[(cut + 1)..]);
        }
    }

    /// <summary>The node at <paramref name="path"/> ('states.produce.step', 'transitions[1].from[0]'), or null.</summary>
    private static JsonNode? Find(JsonNode? node, string path)
    {
        var at = 0;
        while (node is not null && at < path.Length)
        {
            if (path[at] == '[')
            {
                var close = path.IndexOf(']', at);
                var index = int.Parse(path.AsSpan(at + 1, close - at - 1), NumberStyles.None, CultureInfo.InvariantCulture);
                node = node is JsonArray list && index < list.Count ? list[index] : null;
                at = close + 1;
            }
            else
            {
                if (path[at] == '.')
                {
                    at++;
                }

                var end = path.IndexOfAny(['.', '['], at);
                var key = end < 0 ? path[at..] : path[at..end];
                node = node is JsonObject map ? map[key] : null;
                at = end < 0 ? path.Length : end;
            }
        }

        return node;
    }

    private RecipeReading Bind(JsonObject root, string file, ReaderFindings findings)
    {
        Recipe? recipe;
        try
        {
            recipe = (Recipe?)JsonSerializer.Deserialize(root, options.GetTypeInfo(typeof(Recipe)));
        }
        catch (JsonException e)
        {
            // The shape check runs first, so this is a backstop: report it at the node binding names.
            var path = (e.Path ?? "$").TrimStart('$').TrimStart('.');
            findings.Add("RCP-SCHEMA-002", path, $"the recipe doesn't bind: {e.Message}", "fix the value at that place");
            return new RecipeReading(Document: null, findings.All);
        }

        var source = new RecipeSource(file, findings.Positions);
        return new RecipeReading(new RecipeDocument(recipe!, RecipeHashing.Of(recipe!, options), source), []);   // why: the root is a mapping, never JSON null
    }
}
