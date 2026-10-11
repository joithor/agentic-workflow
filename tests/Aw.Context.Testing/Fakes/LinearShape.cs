using System.Text.Json.Nodes;

namespace Aw.Context.Testing.Fakes;

/// <summary>
/// Cuts a recorded value down to what the query selected, as a GraphQL server answers: only the selected fields,
/// under their aliases, recursing into objects and lists. A field the recordings don't have, an object without a
/// sub-selection, or a scalar with one is a 400, so a hand-written query that selects the wrong thing fails here and
/// not against Linear. A nested connection takes 'first' (the recordings hold every child); other nested arguments
/// are refused.
/// </summary>
internal static class LinearShape
{
    public static JsonNode? Select(JsonNode? value, GraphQlField field, string path)
    {
        if (field.Selections is null)
        {
            return value is JsonObject or JsonArray
                ? throw FakeRequestException.Invalid($"Field \"{field.Name}\" at {path} is an object; select its subfields.")
                : value?.DeepClone();
        }

        return value switch
        {
            null => null,
            JsonArray items => new JsonArray([.. items.Select(item => Select(item, field, path))]),
            JsonObject node => Object(node, field.Selections, path),
            _ => throw FakeRequestException.Invalid($"Field \"{field.Name}\" at {path} is a scalar; it takes no selection."),
        };
    }

    private static JsonObject Object(JsonObject node, IReadOnlyList<GraphQlField> selections, string path)
    {
        var shaped = new JsonObject();
        foreach (var child in selections)
        {
            var childPath = path + "." + child.Name;
            if (!node.TryGetPropertyValue(child.Name, out var value))
            {
                throw FakeRequestException.Invalid(
                    $"Cannot query field \"{child.Name}\" at {path}: the fixture's recordings have no such field (fields here: {string.Join(", ", node.Select(p => p.Key))}).");
            }

            shaped[child.ResponseKey] = Select(Nested(value, child, childPath), child, childPath);
        }

        return shaped;
    }

    private static JsonNode? Nested(JsonNode? value, GraphQlField child, string path)
    {
        if (child.Arguments.Count == 0)
        {
            return value;
        }

        if (child.Arguments.Count > 1 || !child.Arguments.TryGetValue("first", out var first)
            || first is not JsonValue n || !n.TryGetValue<int>(out var count) || count < 1
            || value is not JsonObject { } connection || connection["nodes"] is not JsonArray nodes)
        {
            throw FakeRequestException.Invalid($"At {path} the fake Linear server reads only 'first' (a positive Int) on a nested connection.");
        }

        var cut = (JsonObject)connection.DeepClone();
        cut["nodes"] = new JsonArray([.. nodes.Take(count).Select(x => x?.DeepClone())]);
        return cut;
    }
}
