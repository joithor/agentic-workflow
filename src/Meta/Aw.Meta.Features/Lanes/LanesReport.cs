using System.Globalization;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Features.Lanes;

internal static class LanesReport
{
    internal static LanesResponse Of(TeamConfig config, LaneRefresh refresh, IReadOnlyList<LaneHealth>? standing = null)
    {
        var entries = refresh.Map.Entries;
        var unmapped = config.Lanes.Where(lane => !entries.Any(e => string.Equals(e.Lane, lane, StringComparison.OrdinalIgnoreCase))).ToList();
        var parts = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"LANE MAP v{refresh.Map.Version}:"),
        };
        if (entries.Count > 0)
        {
            parts.Add(string.Join(", ", entries.Select(e => $"{e.Lane}={e.SessionName}")));
        }

        if (unmapped.Count > 0)
        {
            parts.Add($"(unmapped: {string.Join(", ", unmapped)})");
        }

        return new LanesResponse(refresh.Map.Version, refresh.Changed, entries, unmapped, string.Join(' ', parts), standing);
    }
}
