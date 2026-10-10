using System.Buffers;
using System.Text;
using System.Text.Json;
using Aw.Judge.Core.Decisions;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>The JSON text columns: decisions.skipped ('[{"provider","reason"}]') and decision_details.probabilities.</summary>
internal static class DecisionJson
{
    internal static string Skipped(IReadOnlyList<SkippedProvider> skipped) => Write(writer =>
    {
        writer.WriteStartArray();
        foreach (var provider in skipped)
        {
            writer.WriteStartObject();
            writer.WriteString("provider", provider.Provider);
            writer.WriteString("reason", DecisionText.Of(provider.Reason));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    internal static IReadOnlyList<SkippedProvider> Skipped(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(element => new SkippedProvider(
            Text(element, "provider"), DecisionText.Reason(Text(element, "reason"))))];
    }

    internal static string? Probabilities(IReadOnlyDictionary<string, double>? probabilities) => probabilities is null ? null : Write(writer =>
    {
        writer.WriteStartObject();
        foreach (var (option, probability) in probabilities)
        {
            writer.WriteNumber(option, probability);
        }

        writer.WriteEndObject();
    });

    internal static IReadOnlyDictionary<string, double>? Probabilities(string? json)
    {
        if (json is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetDouble(), StringComparer.Ordinal);
    }

    private static string Text(JsonElement element, string property) =>
        element.GetProperty(property).GetString() ?? throw new InvalidDataException($"Stored skipped entry has a null '{property}'.");

    private static string Write(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
