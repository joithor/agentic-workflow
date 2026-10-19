using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// The keys of a report's findings (engine P8 D10, A §17.23.13): a 'fact:findings' value that is a JSON list of
/// {file, check, message} gives one key per finding, the digest of its normalized 'file|check|message', so a later round that
/// still has all of them (and perhaps more) made no progress. Any other shape has no keys and keeps the whole-value digest rule.
/// </summary>
public static class FindingKeys
{
    /// <summary>The sorted, distinct keys of <paramref name="findings"/>.</summary>
    /// <param name="findings">The 'fact:findings' value as stored (UTF-8 JSON).</param>
    /// <returns>The keys; null when the value isn't a list of {file, check, message} objects.</returns>
    public static IReadOnlyList<string>? Of(ReadOnlySpan<byte> findings)
    {
        var reader = new Utf8JsonReader(findings);
        JsonDocument document;
        try
        {
            document = JsonDocument.ParseValue(ref reader);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            return document.RootElement.ValueKind == JsonValueKind.Array ? Keys(document.RootElement) : null;
        }
    }

    private static List<string>? Keys(JsonElement list)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var finding in list.EnumerateArray())
        {
            if (Key(finding) is not { } key)
            {
                return null;
            }

            _ = keys.Add(key);
        }

        return [.. keys];
    }

    private static string? Key(JsonElement finding)
    {
        if (finding.ValueKind != JsonValueKind.Object
            || Text(finding, "file") is not { } file || Text(finding, "check") is not { } check || Text(finding, "message") is not { } message)
        {
            return null;
        }

        var normalized = string.Join('|', file.Replace('\\', '/').Trim(), check.Trim(), Message(message));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static string? Text(JsonElement finding, string name) =>
        finding.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Whitespace runs collapse to one space, ends are trimmed and case is dropped, so a re-flowed or re-cased message is the same finding.</summary>
    private static string Message(string message) =>
        string.Join(' ', message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();
}
