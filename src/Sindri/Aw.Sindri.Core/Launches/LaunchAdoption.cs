using System.Globalization;

namespace Aw.Sindri.Core.Launches;

/// <summary>Doctor's adoption line (launcher spec section 9): the opens of the last 30 days by viewer and by agent, from the launches projection.</summary>
public static class LaunchAdoption
{
    /// <summary>How far back the line counts.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(30);

    /// <summary>The line for the started launches among <paramref name="rows"/>.</summary>
    /// <param name="rows">Launches requested inside the window.</param>
    /// <returns>For example 'opens in the last 30 days: 5; by viewer: warp 3, print 2; by agent: claude-code 4, codex 1'.</returns>
    public static string Line(IReadOnlyList<LaunchRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var started = rows.Where(static r => r.State == LaunchState.Started).ToList();
        if (started.Count == 0)
        {
            return "no opens in the last 30 days";
        }

        var viewers = Counts(started.Select(static r => r.Viewer?.Kind ?? "unknown"));
        var agents = Counts(started.Select(static r => r.Target.Value));
        return string.Create(CultureInfo.InvariantCulture, $"opens in the last 30 days: {started.Count}; by viewer: {viewers}; by agent: {agents}");
    }

    private static string Counts(IEnumerable<string> names) => string.Join(", ",
        names.GroupBy(static n => n, StringComparer.Ordinal).OrderByDescending(static g => g.Count()).ThenBy(static g => g.Key, StringComparer.Ordinal)
            .Select(static g => string.Create(CultureInfo.InvariantCulture, $"{g.Key} {g.Count()}")));
}
