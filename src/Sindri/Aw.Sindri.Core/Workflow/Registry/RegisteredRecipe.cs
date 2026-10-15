using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>A registered recipe (spec §4.1): the normalized text kept by its hash, with the key it is approved under.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">The hash of its normalized long form.</param>
/// <param name="Text">The normalized text the hash covers.</param>
/// <param name="Base">The base a generated recipe was made from, or null for a hand-written one.</param>
/// <param name="RegisteredAt">When it was first registered.</param>
public sealed record RegisteredRecipe(RecipeId Id, int Version, RecipeHash Hash, string Text, RecipeRef? Base, DateTimeOffset RegisteredAt)
{
    /// <summary>
    /// The recipe this row's text reads back as. A row registered before urgency, outputs and an input's kind existed
    /// keeps its text (and its hash) as written; the reader fills what such text leaves out, and nothing rehashes it.
    /// </summary>
    /// <param name="json">The JSON options with the recipe records' context.</param>
    /// <returns>The recipe, or the read error.</returns>
    public Result<Recipe> Read(JsonSerializerOptions json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var bytes = Encoding.UTF8.GetBytes(Text);
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(bytes);
        }
        catch (JsonException)
        {
            return AwJson.Deserialize<Recipe>(bytes, json);   // why: the typed read reports malformed text as the kernel error
        }

        if (node is not JsonObject recipe)
        {
            return AwJson.Deserialize<Recipe>(bytes, json);   // why: not an object, so the typed read reports it
        }

        _ = recipe.TryAdd("urgency", Urgencies.Default);
        _ = recipe.TryAdd("outputs", new JsonArray());
        _ = recipe.TryAdd("hash", value: null);
        foreach (var input in recipe["inputs"] is JsonArray inputs ? inputs.OfType<JsonObject>() : [])
        {
            _ = input.TryAdd("kind", value: null);
        }

        return AwJson.Deserialize<Recipe>(Encoding.UTF8.GetBytes(recipe.ToJsonString()), json);
    }
}
