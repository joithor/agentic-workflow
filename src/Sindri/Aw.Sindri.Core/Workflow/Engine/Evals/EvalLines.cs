using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>Writes a case in the §9.2 format: one JSON object per line, members that are null left out.</summary>
public static class EvalLines
{
    private static readonly JsonWriterOptions _writer = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The case's lines, each ending in '\n'.</summary>
    /// <param name="evalCase">The case.</param>
    /// <param name="options">The package's JSON options (they know the eval line records).</param>
    /// <returns>The file's text.</returns>
    public static string Write(EvalCase evalCase, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(evalCase);
        var text = new StringBuilder();
        _ = text.Append(Line(AwJson.Serialize(evalCase.Start, options))).Append('\n');
        foreach (var step in evalCase.Steps)
        {
            _ = text.Append(Line(AwJson.Serialize(step, options))).Append('\n');
        }

        return text.ToString();
    }

    private static string Line(byte[] json)
    {
        var node = JsonNode.Parse(json)!; // why: a serialized record is a JSON object, never the null literal
        DropNulls(node);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, _writer))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void DropNulls(JsonNode node)
    {
        if (node is not JsonObject members)
        {
            return;
        }

        foreach (var name in members.Where(static m => m.Value is null).Select(static m => m.Key).ToList())
        {
            _ = members.Remove(name);
        }

        foreach (var child in members.Select(static m => m.Value).OfType<JsonNode>())
        {
            DropNulls(child);
        }
    }
}
