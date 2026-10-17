using System.Globalization;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>The words of 'context eval backtest': the options it takes, or null for anything else (an option without its value, a date or number that doesn't parse, a stray word).</summary>
/// <param name="Request">The job request the options make.</param>
/// <param name="Detach">Print the job id and return.</param>
/// <param name="Json">Print JSON.</param>
internal sealed record BacktestLine(RunContextBacktestRequest Request, bool Detach, bool Json)
{
    private static readonly string[] _flags = ["--detach", "--json"];
    private static readonly string[] _valued = ["--arms", "--since", "--until", "--limit"];

    internal static BacktestLine? Parse(string[] rest)
    {
        ArgumentNullException.ThrowIfNull(rest);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < rest.Length; i++)
        {
            if (_flags.Contains(rest[i], StringComparer.Ordinal))
            {
                _ = flags.Add(rest[i]);
            }
            else if (_valued.Contains(rest[i], StringComparer.Ordinal) && i + 1 < rest.Length && values.TryAdd(rest[i], rest[i + 1]))
            {
                i++;
            }
            else
            {
                return null;
            }
        }

        return Build(values) is { } request ? new BacktestLine(request, flags.Contains("--detach"), flags.Contains("--json")) : null;
    }

    // Each given option must parse: a date that isn't one, or a limit that isn't a positive number, makes the whole line invalid.
    private static RunContextBacktestRequest? Build(Dictionary<string, string> values) =>
        Option(values, "--since", Date, out var since) && Option(values, "--until", Until, out var until) && Option(values, "--limit", Limit, out var limit)
            ? new RunContextBacktestRequest(values.GetValueOrDefault("--arms"), since, until, limit)
            : null;

    private static bool Option<T>(Dictionary<string, string> values, string key, Func<string, T?> parse, out T? parsed)
        where T : struct
    {
        parsed = values.TryGetValue(key, out var text) ? parse(text) : null;
        return parsed is not null || !values.ContainsKey(key);
    }

    private static int? Limit(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;

    private static DateTimeOffset? Date(string text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;

    // A date alone ('2026-09-30') names the whole day: the window ends at its last tick, so the issues created that day are in. A full timestamp is the instant it says.
    // The last representable day has no next midnight to count back from: it is refused (null) like any other value that names no window.
    private static DateTimeOffset? Until(string text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day == DateOnly.MaxValue ? null : new DateTimeOffset(day.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).AddTicks(-1)
            : Date(text);
}
