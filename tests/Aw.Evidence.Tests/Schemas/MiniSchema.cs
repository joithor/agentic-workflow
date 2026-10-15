using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aw.Evidence.Tests.Schemas;

/// <summary>
/// A small JSON Schema checker for the schemas the exporter writes: $ref into the document, type (one or several), properties, required,
/// additionalProperties, items, enum, pattern, anyOf and oneOf. Anything else in a schema is ignored. It reports where a document
/// breaks the schema, which is all a test needs to say "this produced summary is valid".
/// </summary>
internal sealed class MiniSchema(JsonNode root)
{
    internal static IReadOnlyList<string> Violations(JsonNode schema, JsonNode? document)
    {
        var found = new List<string>();
        new MiniSchema(schema).Check(schema, document, "$", found);
        return found;
    }

    private static bool TypeMatches(JsonNode type, JsonNode? value) =>
        type is JsonArray several ? several.Any(t => t is not null && TypeMatches(t, value)) : Kind(type.GetValue<string>(), value);

    private static bool Kind(string name, JsonNode? value) =>
        name switch
        {
            "null" => value is null,
            "object" => value is JsonObject,
            "array" => value is JsonArray,
            "string" => value is JsonValue s && s.GetValueKind() == JsonValueKind.String,
            "boolean" => value is JsonValue b && b.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
            "integer" => value is JsonValue i && i.GetValueKind() == JsonValueKind.Number && i.TryGetValue<long>(out _),
            "number" => value is JsonValue n && n.GetValueKind() == JsonValueKind.Number,
            _ => true,
        };

    private static string Index(string at, int index) => at + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";

    private void Check(JsonNode schema, JsonNode? value, string at, List<string> found)
    {
        if (schema is JsonValue flag && flag.TryGetValue<bool>(out var allowed))
        {
            if (!allowed)
            {
                found.Add($"{at}: nothing is allowed here");
            }
        }
        else if (schema is JsonObject rules)
        {
            CheckRules(rules, value, at, found);
        }
    }

    private void CheckRules(JsonObject rules, JsonNode? value, string at, List<string> found)
    {
        if (rules["$ref"]?.GetValue<string>() is { } reference)
        {
            Check(Resolve(reference), value, at, found);
            return;
        }

        if (rules["type"] is { } type && !TypeMatches(type, value))
        {
            found.Add($"{at}: expected type {type.ToJsonString()}, found {(value is null ? "null" : value.GetValueKind().ToString())}");
            return;
        }

        CheckScalar(rules, value, at, found);
        CheckAlternatives(rules, value, at, found);
        CheckContainer(rules, value, at, found);
    }

    private static void CheckScalar(JsonObject rules, JsonNode? value, string at, List<string> found)
    {
        if (rules["enum"] is JsonArray choices && !choices.Any(c => JsonNode.DeepEquals(c, value)))
        {
            found.Add($"{at}: {value?.ToJsonString()} is not one of {choices.ToJsonString()}");
        }

        if (rules["pattern"]?.GetValue<string>() is { } pattern && value is JsonValue text && text.TryGetValue<string>(out var s)
            && !Regex.IsMatch(s, pattern, RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            found.Add($"{at}: \"{s}\" does not match {pattern}");
        }
    }

    private void CheckAlternatives(JsonObject rules, JsonNode? value, string at, List<string> found)
    {
        foreach (var keyword in new[] { "anyOf", "oneOf" })
        {
            if (rules[keyword] is JsonArray alternatives && !alternatives.Any(a => a is not null && Matches(a, value, at)))
            {
                found.Add($"{at}: matches none of the {keyword} alternatives");
            }
        }
    }

    private bool Matches(JsonNode schema, JsonNode? value, string at)
    {
        var found = new List<string>();
        Check(schema, value, at, found);
        return found.Count == 0;
    }

    private void CheckContainer(JsonObject rules, JsonNode? value, string at, List<string> found)
    {
        if (value is JsonObject obj)
        {
            CheckRequired(rules, obj, at, found);
            CheckMembers(rules, obj, at, found);
        }
        else if (value is JsonArray array && rules["items"] is { } items)
        {
            for (var i = 0; i < array.Count; i++)
            {
                Check(items, array[i], Index(at, i), found);
            }
        }
    }

    private static void CheckRequired(JsonObject rules, JsonObject obj, string at, List<string> found)
    {
        foreach (var name in (rules["required"] as JsonArray)?.Select(static n => n!.GetValue<string>()) ?? [])
        {
            if (!obj.ContainsKey(name))
            {
                found.Add($"{at}: missing required property \"{name}\"");
            }
        }
    }

    private void CheckMembers(JsonObject rules, JsonObject obj, string at, List<string> found)
    {
        var closed = rules["additionalProperties"] is JsonValue flag && flag.TryGetValue<bool>(out var open) && !open;
        foreach (var (name, member) in obj)
        {
            if (rules["properties"]?[name] is { } memberSchema)
            {
                Check(memberSchema, member, $"{at}.{name}", found);
            }
            else if (closed)
            {
                found.Add($"{at}: unknown property \"{name}\"");
            }
        }
    }

    private JsonNode Resolve(string reference)
    {
        var current = root;
        foreach (var part in reference.TrimStart('#', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current[part] ?? throw new InvalidOperationException($"schema reference {reference} does not resolve");
        }

        return current;
    }
}
