using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>A token count as recipes and commands write it: '500k', '1M', or a plain whole number.</summary>
internal static class TokenCount
{
    /// <summary>Reads a count.</summary>
    /// <param name="text">The text, for example "1M".</param>
    /// <param name="tokens">The tokens, at least 1.</param>
    /// <returns>True when the text is a whole count of at least 1 token.</returns>
    internal static bool TryParse(string? text, out int tokens)
    {
        tokens = 0;
        var trimmed = text?.Trim() ?? string.Empty;
        var (scale, digits) = trimmed.EndsWith('M') ? (1_000_000L, trimmed[..^1]) : trimmed.EndsWith('k') ? (1_000L, trimmed[..^1]) : (1L, trimmed);
        if (digits.Length == 0 || !long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count > int.MaxValue || count * scale is < 1 or > int.MaxValue)
        {
            return false;
        }

        tokens = (int)(count * scale);
        return true;
    }
}
