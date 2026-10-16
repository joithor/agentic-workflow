using System.Globalization;

namespace Aw.Meta.Core.Watch;

/// <summary>Reads the watch's durations: "29m", "90s", "2h" or a bare number of seconds.</summary>
public static class WatchDuration
{
    /// <summary>Parses a duration.</summary>
    /// <param name="text">The text.</param>
    /// <param name="duration">The duration.</param>
    /// <returns>Whether the text is a positive duration.</returns>
    public static bool TryParse(string text, out TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(text);
        duration = default;
        var trimmed = text.Trim();
        var unit = trimmed.Length > 0 ? trimmed[^1] : ' ';
        var seconds = unit switch { 'h' => 3600, 'm' => 60, 's' => 1, _ => 1 };
        var digits = char.IsAsciiLetter(unit) ? trimmed[..^1] : trimmed;
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount <= 0 || (char.IsAsciiLetter(unit) && unit is not ('h' or 'm' or 's')))
        {
            return false;
        }

        duration = TimeSpan.FromSeconds((long)amount * seconds);
        return true;
    }
}
