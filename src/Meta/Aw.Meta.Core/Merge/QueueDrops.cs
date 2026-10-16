using System.Globalization;

namespace Aw.Meta.Core.Merge;

/// <summary>When each pull request's merge-queue entry was dropped, kept as "pr=ticks" pairs separated by commas. A ready message newer than the drop queues the pull request again.</summary>
public static class QueueDrops
{
    /// <summary>Reads the stored value; malformed pairs are ignored.</summary>
    /// <param name="stored">The stored value, or null.</param>
    /// <returns>The drop time per pull request number.</returns>
    public static IReadOnlyDictionary<int, DateTimeOffset> Parse(string? stored)
    {
        var drops = new Dictionary<int, DateTimeOffset>();
        foreach (var pair in (stored ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=');
            if (parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var pr)
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && ticks <= DateTimeOffset.MaxValue.UtcTicks)
            {
                drops[pr] = new DateTimeOffset(ticks, TimeSpan.Zero);
            }
        }

        return drops;
    }

    /// <summary>The stored value with one more drop.</summary>
    /// <param name="stored">The stored value, or null.</param>
    /// <param name="pr">The pull request.</param>
    /// <param name="at">When its entry was dropped.</param>
    /// <returns>The new value.</returns>
    public static string With(string? stored, int pr, DateTimeOffset at)
    {
        var drops = new Dictionary<int, DateTimeOffset>(Parse(stored)) { [pr] = at };
        return string.Join(',', drops.OrderBy(static d => d.Key).Select(static d => string.Create(CultureInfo.InvariantCulture, $"{d.Key}={d.Value.UtcTicks}")));
    }
}
