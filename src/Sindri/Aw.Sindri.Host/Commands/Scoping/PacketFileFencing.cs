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

    /// <summary>The file with its text fenced. A JSON file that is not JSON comes back as text (format 'text'), wrapped whole.</summary>
    internal static PacketFile Fence(PacketFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file.Format switch
        {
            "json" => Json(file),
            "jsonl" => file with { Text = Lines(file.Text) },
            _ => file with { Text = Whole(file.Text) },
        };
    }

    private static string Whole(string text) => UntrustedFence.Wrap(Source, author: null, text);

    private static PacketFile Json(PacketFile file)
    {
        var text = Json(file.Text);
        return text is null ? file with { Text = Whole(file.Text), Format = "text" } : file with { Text = text };
    }

    private static string? Json(string text)
    {
        try
        {
            var written = Write(Fence(JsonNode.Parse(text), owner: false));
            return written.Length == 0 ? null : written;
        }
        catch (JsonException)
        {
            return null;   // boundary: the file is text a model's output went into
        }
    }

    private static string Lines(string text)
    {
        var lines = text.Split('\n').Where(static l => l.Length > 0).Select(l => Json(l) ?? Whole(l));
        return string.Join('\n', lines) + "\n";
    }

    private static JsonNode? Fence(JsonNode? node, bool owner)
    {
        return node switch
        {
            JsonObject obj => FenceObject(obj, owner),
            JsonArray array => new JsonArray([.. array.Select(item => Fence(item, owner))]),
            JsonValue value when !owner && value.TryGetValue<string>(out var text) => JsonValue.Create(Whole(text)),
            _ => Copy(node),
        };
    }

    private static JsonNode? Copy(JsonNode? node) => node?.DeepClone();

    private static JsonObject FenceObject(JsonObject obj, bool owner)
    {
        var mine = owner || (obj["trust"] is JsonValue trust && trust.TryGetValue<string>(out var label) && string.Equals(label, "owner", StringComparison.Ordinal));
        var copy = new JsonObject();
        foreach (var (name, value) in obj)
        {
            copy[name] = string.Equals(name, "trust", StringComparison.Ordinal) ? Copy(value) : Fence(value, mine);
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
