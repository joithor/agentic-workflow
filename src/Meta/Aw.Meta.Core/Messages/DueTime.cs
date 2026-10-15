using System.Globalization;

namespace Aw.Meta.Core.Messages;

/// <summary>Reads the time a deferred reply reopens at: "+30m", "+2h", "+1d" from now, or an ISO 8601 time.</summary>
public static class DueTime
{
    /// <summary>Parses a due time.</summary>
    /// <param name="text">The text.</param>
    /// <param name="now">What a relative time counts from.</param>
    /// <returns>The time, or null when the text is neither form.</returns>
    public static DateTimeOffset? Parse(string text, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        if (trimmed.Length > 2 && trimmed[0] == '+' && char.IsAsciiDigit(trimmed[1]))
        {
            return Relative(trimmed, now);
        }

        return DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at) ? at : null;
    }

    private static DateTimeOffset? Relative(string text, DateTimeOffset now)
    {
        if (!int.TryParse(text[1..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        return text[^1] switch
        {
            's' => now.AddSeconds(amount),
            'm' => now.AddMinutes(amount),
            'h' => now.AddHours(amount),
            'd' => now.AddDays(amount),
            _ => null,
        };
    }
}
