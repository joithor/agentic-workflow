using System.Globalization;
using System.Text.Json;

namespace Aw.Kernel.Json;

/// <summary>The timestamp form AwJson writes and reads: UTC ISO-8601, round-trip ('O') format, explicit offset on read.</summary>
internal static class UtcTimestamps
{
    public static string Format(DateTime utc) => utc.ToString("O", CultureInfo.InvariantCulture);

    public static DateTimeOffset Read(ref Utf8JsonReader reader)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return text is not null && HasExplicitOffset(text) && reader.TryGetDateTimeOffset(out var value)
            ? value.ToUniversalTime()
            : throw new JsonException($"Expected an ISO-8601 timestamp with 'Z' or an offset (2026-10-10T06:00:00.0000000Z), found '{text}'.");
    }

    // Without 'Z' or an offset the reader would assume the machine's time zone.
    private static bool HasExplicitOffset(string text) =>
        text.EndsWith('Z') || (text.Length > 6 && text[^6] is '+' or '-' && text[^3] == ':');
}
