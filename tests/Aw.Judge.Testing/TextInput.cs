using System.Text.Json.Nodes;

namespace Aw.Judge.Testing;

/// <summary>The test question's input: {"text": string}; anything else is invalid, unknown fields are dropped.</summary>
/// <param name="Text">The text.</param>
public sealed record TextInput(string Text)
{
    /// <summary>Reads {"text": string}.</summary>
    /// <param name="node">The raw input.</param>
    /// <returns>The input, or null when it isn't an object with a string text.</returns>
    public static TextInput? Parse(JsonNode? node) =>
        node is JsonObject obj && obj["text"] is JsonValue text && text.TryGetValue<string>(out var value) ? new TextInput(value) : null;

    /// <summary>The input as JSON.</summary>
    /// <returns>{"text": …}.</returns>
    public JsonNode ToJson() => new JsonObject { ["text"] = Text };
}
