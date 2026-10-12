using System.Text.Json;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// An eval line's fact content as the instance holds it (spec §5.1): a string's own text, any other JSON value's
/// text, referenced as 'blob:{sha256}', so equal content gives equal digests (no-progress compares them).
/// </summary>
public static class EvalFacts
{
    /// <summary>The stored reference for <paramref name="content"/>.</summary>
    /// <param name="content">The content as the eval line writes it.</param>
    /// <returns>The fact value.</returns>
    public static FactValue Value(JsonElement content) =>
        throw new NotSupportedException("P3 unit (f): EvalFacts.Value");

    /// <summary>The content's text: a JSON string's value, otherwise the JSON itself.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The text.</returns>
    public static string Text(JsonElement content) =>
        throw new NotSupportedException("P3 unit (f): EvalFacts.Text");

    /// <summary>A JSON string holding <paramref name="text"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The element.</returns>
    public static JsonElement OfText(string text) =>
        throw new NotSupportedException("P3 unit (f): EvalFacts.OfText");
}
