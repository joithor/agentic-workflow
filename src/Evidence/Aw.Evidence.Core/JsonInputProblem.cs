using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aw.Evidence.Core;

/// <summary>
/// Turns a JsonException into "&lt;JSON path&gt;: &lt;problem&gt;". The path comes from the exception (plus the part below a
/// converter that read a nested document, from its data); the problem from the kind of failure and the offending value
/// read back from the document, so no CLR type name reaches the reader of the message.
/// </summary>
internal static partial class JsonInputProblem
{
    private const string SubPathKey = "aw.input.subPath";
    private const string ProblemKey = "aw.input.problem";

    /// <summary>An error for a converter to throw: <paramref name="problem"/> at <paramref name="subPath"/> below the value it reads.</summary>
    /// <param name="subPath">The path below the converter's value ("$" for the value itself).</param>
    /// <param name="problem">The problem in the input's terms.</param>
    /// <returns>The exception; its Path stays unset so the serializer fills in where the value sits.</returns>
    internal static JsonException At(string subPath, string problem)
    {
        var ex = new JsonException(problem);
        ex.Data[SubPathKey] = subPath;
        ex.Data[ProblemKey] = problem;
        return ex;
    }

    /// <summary>Wraps a failure a converter met reading a nested document, keeping where in that document it happened.</summary>
    /// <param name="inner">The nested failure.</param>
    /// <returns>The exception; its Path stays unset so the serializer fills in where the value sits.</returns>
    internal static JsonException Nested(JsonException inner)
    {
        var ex = new JsonException(inner.Message, inner);
        ex.Data[SubPathKey] = inner.Path ?? "$";
        if (inner.Data[ProblemKey] is string problem)
        {
            ex.Data[ProblemKey] = problem;
        }

        return ex;
    }

    internal static string Describe(JsonException ex, ReadOnlySpan<byte> utf8Json)
    {
        var subPath = ex.Data[SubPathKey] as string;
        if (subPath is null && ex.InnerException is JsonException)
        {
            return string.Create(CultureInfo.InvariantCulture, $"$: not valid JSON (line {ex.LineNumber + 1}, column {ex.BytePositionInLine + 1})");
        }

        var path = (ex.Path ?? "$") + (subPath is null ? string.Empty : subPath[1..]);
        if (ex.Data[ProblemKey] is string problem)
        {
            return $"{path}: {problem}";
        }

        var cause = subPath is not null && ex.InnerException is JsonException inner ? inner : ex;
        return $"{path}: {Problem(cause.Message, path, utf8Json)}";
    }

    private static string Problem(string message, string path, ReadOnlySpan<byte> utf8Json)
    {
        var including = message.IndexOf("including:", StringComparison.Ordinal);
        if (message.Contains("missing required properties", StringComparison.Ordinal) && including >= 0)
        {
            var names = Quoted.Matches(message[including..]).Select(static m => $"\"{m.Groups["name"].Value}\"").ToList();
            return names.Count == 1 ? $"missing required property {names[0]}" : $"missing required properties {string.Join(", ", names)}";
        }

        if (message.Contains("could not be mapped to any", StringComparison.Ordinal))
        {
            return "unknown property";
        }

        return JsonPathValue.Find(utf8Json, path) switch
        {
            null => "is not valid here",
            { Kind: JsonValueKind.Null } => "must not be null",
            { Text: var text } => $"{Shorten(text)} is not a valid value",
        };
    }

    private static string Shorten(string text) => text.Length <= 60 ? text : string.Concat(text.AsSpan(0, 57), "...");

    [GeneratedRegex("'(?<name>[^']*)'", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Quoted { get; }
}
