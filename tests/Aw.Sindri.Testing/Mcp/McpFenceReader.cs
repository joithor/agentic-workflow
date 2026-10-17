using System.Text.Json;

namespace Aw.Sindri.Testing.Mcp;

/// <summary>
/// Reads a fenced MCP result back: every &lt;untrusted source=… [author=…]&gt;…&lt;/untrusted&gt; string is unwrapped and
/// unescaped, and the trust members are dropped. For tests that compare a tool's structuredContent with the HTTP body.
/// </summary>
public static class McpFenceReader
{
    private const string Open = "<untrusted source=\"";
    private const string Close = "</untrusted>";

    /// <summary>The element with its fences removed.</summary>
    /// <param name="fenced">A tool's structuredContent.</param>
    /// <returns>The same JSON as the route's body.</returns>
    public static JsonElement Unfence(JsonElement fenced)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(fenced, writer);
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    /// <summary>The author attribute of a fenced string, or null when it has none (or is not fenced).</summary>
    /// <param name="text">A string from a tool result.</param>
    /// <returns>The unescaped author.</returns>
    public static string? AuthorOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        const string Marker = "\" author=\"";
        var head = text.IndexOf('>', StringComparison.Ordinal);
        if (!text.StartsWith(Open, StringComparison.Ordinal) || head < 0)
        {
            return null;
        }

        var at = text.IndexOf(Marker, StringComparison.Ordinal);
        return at < 0 || at > head ? null : Unescape(text[(at + Marker.Length)..(head - 1)]);
    }

    private static void Write(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().Where(p => !string.Equals(p.Name, "trust", StringComparison.Ordinal)))
            {
                writer.WritePropertyName(property.Name);
                Write(property.Value, writer);
            }

            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
            {
                Write(item, writer);
            }

            writer.WriteEndArray();
        }
        else if (element.ValueKind == JsonValueKind.String && TryUnwrap(element.GetString() ?? string.Empty, out var body))
        {
            writer.WriteStringValue(body);
        }
        else
        {
            element.WriteTo(writer);
        }
    }

    private static bool TryUnwrap(string text, out string body)
    {
        body = string.Empty;
        var head = text.IndexOf('>', StringComparison.Ordinal);
        if (!text.StartsWith(Open, StringComparison.Ordinal) || !text.EndsWith(Close, StringComparison.Ordinal) || head < 0)
        {
            return false;
        }

        body = Unescape(text[(head + 1)..^Close.Length]);
        return true;
    }

    private static string Unescape(string text) =>
        text.Replace("&lt;", "<", StringComparison.Ordinal).Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal).Replace("&amp;", "&", StringComparison.Ordinal);
}
