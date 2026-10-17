using System.Globalization;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>Parses the /proc text formats this package reads.</summary>
internal static class ProcFormat
{
    internal sealed record StatFields(string Name, char State, int Ppid, long StartTicks, long RssPages);

    internal sealed record MemFields(long Available, long SwapUsed, long Total, long SwapTotal = 0);

    // "pid (comm) S ppid ...": comm may hold spaces and parentheses, so split after the last ')'.
    internal static StatFields? Stat(string stat)
    {
        var open = stat.IndexOf('(', StringComparison.Ordinal);
        var close = stat.LastIndexOf(')');
        if (open < 0 || close < open)
        {
            return null;
        }

        var rest = stat[(close + 2)..].Split(' ');
        const int Needed = 22;
        return rest.Length < Needed
            ? null
            : new StatFields(
                stat[(open + 1)..close],
                rest[0][0],
                int.Parse(rest[1], CultureInfo.InvariantCulture),
                long.Parse(rest[19], CultureInfo.InvariantCulture),
                long.Parse(rest[21], CultureInfo.InvariantCulture));
    }

    internal static MemFields MemInfo(string meminfo)
    {
        long Value(string key) =>
            meminfo.Split('\n').FirstOrDefault(l => l.StartsWith(key + ":", StringComparison.Ordinal)) is { } line
                ? long.Parse(line[(key.Length + 1)..].Replace("kB", string.Empty, StringComparison.Ordinal).Trim(), CultureInfo.InvariantCulture)
                : 0;

        return new MemFields(Value("MemAvailable"), Value("SwapTotal") - Value("SwapFree"), Value("MemTotal"), Value("SwapTotal"));
    }
}
