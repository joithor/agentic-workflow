using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Context.Core.Trust;
using Aw.Sindri.Features.Scoping;

namespace Aw.Sindri.Host.Commands.Scoping;

/// <summary>
/// Fences a packet file for a session caller (scoping spec API9, W7): every JSON string value is wrapped in the pack fence unless it sits in an object
/// whose trust is 'owner' (a decision the owner made) or is a trust label; each JSON-lines line is treated as a JSON object; a markdown file is wrapped
/// whole. A file that is not the JSON it claims to be is wrapped whole too (fail closed). A human gets the file as it is.
/// </summary>
internal static class PacketFileFencing
{
    private const string Source = "scoping";

    /// <summary>The file with its text fenced.</summary>
    internal static PacketFile Fence(PacketFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file with { Text = file.Format switch { "json" => Json(file.Text), "jsonl" => Lines(file.Text), _ => Whole(file.Text) } };
    }

    private static string Whole(string text) => UntrustedFence.Wrap(Source, author: null, text);

    private static string Json(string text)
    {
        try
        {
            return JsonNode.Parse(text) is { } node ? Write(Fence(node, owner: false)) : Whole(text);
        }
        catch (JsonException)
        {
            return Whole(text);   // boundary: the file is text a model's output went into
        }
    }

    private static string Lines(string text)
    {
        var lines = text.Split('\n').Where(static l => l.Length > 0).Select(Json);
        return string.Join('\n', lines) + "\n";
    }

    private static JsonNode? Fence(JsonNode? node, bool owner)
    {
        switch (node)
        {
            case JsonObject obj:
                return FenceObject(obj, owner);
            case JsonArray array:
                return new JsonArray([.. array.Select(item => Fence(item, owner))]);
            case JsonValue value when !owner && value.TryGetValue<string>(out var text):
                return JsonValue.Create(Whole(text));
            default:
                return node?.DeepClone();
        }
    }

    private static JsonObject FenceObject(JsonObject obj, bool owner)
    {
        var mine = owner || string.Equals(obj["trust"]?.GetValue<string>(), "owner", StringComparison.Ordinal);
        var copy = new JsonObject();
        foreach (var (name, value) in obj)
        {
            copy[name] = string.Equals(name, "trust", StringComparison.Ordinal) ? value?.DeepClone() : Fence(value, mine);
        }

        return copy;
    }

    private static string Write(JsonNode? node)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            node?.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
