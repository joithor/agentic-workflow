using System.Text.Json;

namespace Aw.Evidence.Core.Data;

/// <summary>A JSON Pointer (RFC 6901): its own small evaluator, so a script's assertions never depend on a library's dialect.</summary>
public static class JsonPointer
{
    /// <summary>Why a pointer is malformed, or null when it is well-formed ("" or '/'-led, '~0' and '~1' the only escapes).</summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The problem, or null.</returns>
    public static string? Problem(string reference) => throw new NotSupportedException();

    /// <summary>The value a pointer names in a document.</summary>
    /// <param name="document">The root element.</param>
    /// <param name="reference">A well-formed reference.</param>
    /// <returns>The element, or null when it does not resolve (an array index is digits without a leading zero; '-' never resolves).</returns>
    public static JsonElement? Resolve(JsonElement document, string reference) => throw new NotSupportedException();
}
