using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The words status uses for times and counts (engine spec §6.4): durations as '1d 22h 48m' or '40s' under a minute,
/// clock times as 'hh:mm' in UTC (with the date once a day or more has passed), token counts as '200k' or '1M'. Plain ASCII.
/// </summary>
public static class StatusText
{
    /// <summary>A duration as §6.4 writes it: '1d 22h 48m', or '40s' under a minute; never negative.</summary>
    /// <param name="span">The duration.</param>
    /// <returns>The text.</returns>
    public static string Duration(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, (int)span.TotalSeconds)}s");
        }

        var parts = new List<string>(3);
        if (span.Days > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Days}d"));
        }

        if (span.Hours > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Hours}h"));
        }

        if (span.Minutes > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}m"));
        }

        return string.Join(' ', parts);
    }

    /// <summary>A clock time in UTC: 'hh:mm', or 'MM-dd hh:mm' once <paramref name="now"/> is a day or more later.</summary>
    /// <param name="at">The time.</param>
    /// <param name="now">The time it is read at.</param>
    /// <returns>The text.</returns>
    public static string Clock(DateTimeOffset at, DateTimeOffset now) =>
        at.UtcDateTime.ToString(now - at >= TimeSpan.FromDays(1) ? "MM-dd HH:mm" : "HH:mm", CultureInfo.InvariantCulture);

    /// <summary>A token count: '0', '950', '1.5k', '200k', '1M'.</summary>
    /// <param name="tokens">The count.</param>
    /// <returns>The text.</returns>
    public static string Tokens(int tokens) => tokens switch
    {
        > 0 when tokens % 1_000_000 == 0 => string.Create(CultureInfo.InvariantCulture, $"{tokens / 1_000_000}M"),
        > 0 when tokens % 1_000 == 0 => string.Create(CultureInfo.InvariantCulture, $"{tokens / 1_000}k"),
        >= 1_000 => string.Create(CultureInfo.InvariantCulture, $"{tokens / 1_000.0:0.#}k"),
        _ => tokens.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>The first 12 hex digits of a recipe hash, as status and the CLI name a recipe ('id@hash12').</summary>
    /// <param name="hash">The hash, with or without its 'sha256:' prefix.</param>
    /// <returns>The 12 digits.</returns>
    public static string Hash12(string hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        var digits = hash[(hash.IndexOf(':', StringComparison.Ordinal) + 1)..];
        return digits.Length > 12 ? digits[..12] : digits;
    }
}
