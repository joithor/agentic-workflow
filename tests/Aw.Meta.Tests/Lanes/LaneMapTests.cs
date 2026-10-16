using Aw.Meta.Core.Lanes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Lanes;

public sealed class LaneMapTests
{
    private static readonly string[] _lanes = ["meta", "api", "engine", "ui"];

    [Fact, Trait("Lane", "unit")]
    public void A_live_session_named_team_lane_maps_directly()
    {
        var live = new[] { A.Live("t1-api", "s-1"), A.Live("t1-ui", "s-2"), A.Live("other", "s-3") };

        var map = LaneMap.Refresh("t1", _lanes, live, [], LaneMapState.Empty);

        Assert.Equal(["api", "ui"], map.Entries.Select(e => e.Lane), StringComparer.Ordinal);
        Assert.Equal(["t1-api", "t1-ui"], map.Entries.Select(e => e.SessionName), StringComparer.Ordinal);
        Assert.All(map.Entries, e => Assert.Equal(LaneSource.Direct, e.Source));
        Assert.Equal("s-1", map.Entries[0].Session?.Value);
        Assert.Equal(1, map.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_latest_announcement_from_a_live_session_maps_a_lane()
    {
        var live = new[] { A.Live("agentic-workflow-4c", "s-4"), A.Live("agentic-workflow-9a", "s-9") };
        var announcements = new[]
        {
            A.Inbox("m1", name: "agentic-workflow-4c", body: "engine lane here", minute: 1),
            A.Inbox("m2", name: "agentic-workflow-9a", body: "engine lane here, restarted", minute: 5),
        };

        var map = LaneMap.Refresh("t1", _lanes, live, announcements, LaneMapState.Empty);

        var entry = Assert.Single(map.Entries);
        Assert.Equal(("engine", "agentic-workflow-9a", LaneSource.Announcement), (entry.Lane, entry.SessionName, entry.Source));
        Assert.Equal("s-9", entry.Session?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_announcement_from_a_session_that_is_not_live_is_ignored()
    {
        var announcements = new[] { A.Inbox("m1", name: "gone-1", body: "api lane here") };

        var map = LaneMap.Refresh("t1", _lanes, [], announcements, LaneMapState.Empty);

        Assert.Empty(map.Entries);
        Assert.Equal(0, map.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_direct_session_beats_an_announcement_and_an_unknown_lane_is_not_mapped()
    {
        var live = new[] { A.Live("t1-api", "s-1"), A.Live("x-1", "s-2") };
        var announcements = new[] { A.Inbox("m1", name: "x-1", body: "api lane here"), A.Inbox("m2", name: "x-1", body: "docs lane here") };

        var map = LaneMap.Refresh("t1", _lanes, live, announcements, LaneMapState.Empty);

        var entry = Assert.Single(map.Entries);
        Assert.Equal(("api", LaneSource.Direct), (entry.Lane, entry.Source));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_version_rises_only_when_the_assignments_change()
    {
        var first = LaneMap.Refresh("t1", _lanes, [A.Live("t1-api", "s-1")], [], LaneMapState.Empty);
        var same = LaneMap.Refresh("t1", _lanes, [A.Live("t1-api", "s-1")], [], first);
        var moved = LaneMap.Refresh("t1", _lanes, [A.Live("t1-api", "s-7")], [], same);
        var grown = LaneMap.Refresh("t1", _lanes, [A.Live("t1-api", "s-7"), A.Live("t1-ui", "s-8")], [], moved);

        Assert.Equal([1, 1, 2, 3], [first.Version, same.Version, moved.Version, grown.Version]);
    }
}
