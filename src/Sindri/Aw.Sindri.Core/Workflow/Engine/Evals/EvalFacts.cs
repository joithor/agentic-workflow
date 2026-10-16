using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aw.Sindri.Core.Workflow.Engine.Evals;

/// <summary>
/// An eval line's fact content as the instance holds it (spec §5.1): a string's own text, any other JSON value's
/// text, referenced as 'blob:{sha256}', so equal content gives equal digests (no-progress compares them).
/// </summary>
internal static class EvalFacts
{
    /// <summary>The stored reference for <paramref name="content"/>.</summary>
    /// <param name="content">The content as the eval line writes it.</param>
    /// <returns>The fact value.</returns>
    public static FactValue Value(JsonElement content)
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Text(content))));
        return new FactValue("blob:" + digest, digest, HeadSha: null);
    }

    /// <summary>The stored reference for an engine-filled decision: its fields, one per line, give the digest.</summary>
    /// <param name="decision">The decision the engine wrote.</param>
    /// <returns>The fact value.</returns>
    public static FactValue Value(Blocks.DecisionFact decision)
    {
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{decision.Choice}\n{decision.Provider}\n{decision.Actor}")));
        return new FactValue("blob:" + digest, digest, HeadSha: null);
    }

    /// <summary>The content's text: a JSON string's value, otherwise the JSON itself.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The text.</returns>
    public static string Text(JsonElement content) =>
        content.ValueKind == JsonValueKind.String ? content.GetString() ?? string.Empty : content.GetRawText();

    /// <summary>A JSON string holding <paramref name="text"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The element.</returns>
    public static JsonElement OfText(string text)
    {
        using var document = JsonDocument.Parse("\"" + JsonEncodedText.Encode(text).Value + "\"");
        return document.RootElement.Clone();
    }
}
