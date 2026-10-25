using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Meta.Core.Usage;

/// <summary>Finds a usage-limit message at the start of a line and reads its kind and reset time.</summary>
internal static partial class UsageLimitParser
{
    private static readonly string[] _months = ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    /// <summary>Whether the text starts (after any marks and spaces) with a usage-limit message.</summary>
    /// <param name="text">A line, or the text of a message.</param>
    /// <returns>True for "You've hit your … limit" and "Claude usage limit reached".</returns>
    internal static bool Mentions(string text) => Message.IsMatch(text);

    /// <summary>Parses the usage-limit message the text starts with.</summary>
    /// <param name="text">A line (or two) from a pane or a transcript.</param>
    /// <param name="reference">When the message was shown; a bare clock time means its next occurrence after this.</param>
    /// <param name="local">The zone of a reset time that names none.</param>
    /// <param name="seenNow">Whether the reference is only when it was looked at (a pane), not when the message was written: a bare clock time is then its occurrence nearest to the reference, so it does not slide to tomorrow once passed.</param>
    /// <returns>The stop; null when the text holds no usage-limit message.</returns>
    internal static UsageStop? Parse(string text, DateTimeOffset reference, TimeZoneInfo local, bool seenNow = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(local);
        var message = Message.Match(text);
        if (!message.Success)
        {
            return null;
        }

        var what = message.Groups["what"].Value.ToUpperInvariant();
        var kind = what.Contains("SESSION", StringComparison.Ordinal) ? UsageKind.Session : what.Contains("WEEK", StringComparison.Ordinal) ? UsageKind.Weekly : UsageKind.Other;
        return new UsageStop(kind, ResetsAt(text[message.Length..], reference, local, seenNow), text.Trim());
    }

    private static DateTimeOffset? ResetsAt(string rest, DateTimeOffset reference, TimeZoneInfo local, bool seenNow)
    {
        var reset = Reset.Match(rest);
        var clock = reset.Success ? Clock.Match(reset.Groups["when"].Value) : Match.Empty;
        if (!clock.Success || ZoneOf(reset, local) is not { } zone || WallOf(clock) is not { } wall)
        {
            return null;
        }

        var at = TimeZoneInfo.ConvertTime(reference, zone);
        return clock.Groups["mon"].Success ? OnDate(clock, wall, at, zone, reference) : NextClock(wall, at, zone, reference, seenNow);
    }

    // The zone the message names; the local one when it names none; null when it names one that does not exist.
    private static TimeZoneInfo? ZoneOf(Match reset, TimeZoneInfo local) =>
        reset.Groups["zone"].Success ? Zone(reset.Groups["zone"].Value.Trim()) : local;

    private static Wall? WallOf(Match clock)
    {
        var hour = int.Parse(clock.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minute = clock.Groups["m"].Success ? int.Parse(clock.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        if (hour is < 1 or > 12 || minute > 59)
        {
            return null;
        }

        var pm = clock.Groups["ap"].Value.StartsWith('p') || clock.Groups["ap"].Value.StartsWith('P');
        return new Wall((hour % 12) + (pm ? 12 : 0), minute);
    }

    private static DateTimeOffset? OnDate(Match clock, Wall wall, DateTimeOffset at, TimeZoneInfo zone, DateTimeOffset reference)
    {
        var month = Array.IndexOf(_months, clock.Groups["mon"].Value[..3].ToLowerInvariant()) + 1;
        var day = int.Parse(clock.Groups["day"].Value, CultureInfo.InvariantCulture);
        try
        {
            var found = Resolve(new DateTime(at.Year, month, day, wall.Hour, wall.Minute, 0, DateTimeKind.Unspecified), zone);
            return found < reference.AddDays(-1) ? Resolve(new DateTime(at.Year + 1, month, day, wall.Hour, wall.Minute, 0, DateTimeKind.Unspecified), zone) : found;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static DateTimeOffset NextClock(Wall wall, DateTimeOffset at, TimeZoneInfo zone, DateTimeOffset reference, bool seenNow)
    {
        var today = at.Date.AddHours(wall.Hour).AddMinutes(wall.Minute);
        if (seenNow)
        {
            return new[] { -1, 0, 1 }.Select(d => Resolve(today.AddDays(d), zone)).OrderBy(c => (c - reference).Duration()).ThenByDescending(static c => c).First();
        }

        var found = Resolve(today, zone);
        return found < reference ? Resolve(today.AddDays(1), zone) : found;
    }

    private static DateTimeOffset Resolve(DateTime wall, TimeZoneInfo zone)
    {
        var fixedWall = zone.IsInvalidTime(wall) ? wall.AddHours(1) : wall;
        return new DateTimeOffset(fixedWall, zone.GetUtcOffset(fixedWall));
    }

    private static TimeZoneInfo? Zone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    private sealed record Wall(int Hour, int Minute);

    [GeneratedRegex(@"^[^A-Za-z0-9]*(?:you['’]ve\s+hit\s+your\s+(?<what>[A-Za-z0-9 ._-]*?)\s*limit|(?:claude\s+)?usage\s+limit\s+reached)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Message { get; }

    [GeneratedRegex(@"\bresets?\b(?:\s+at)?\s*(?<when>[^()]*)(?:\((?<zone>[^)]*)\))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Reset { get; }

    [GeneratedRegex(@"^\s*(?:(?<mon>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\.?\s+(?<day>\d{1,2})(?:st|nd|rd|th)?\s*(?:,|at)?\s*)?(?<h>\d{1,2})(?::(?<m>\d{2}))?\s*(?<ap>[ap]m)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Clock { get; }
}
