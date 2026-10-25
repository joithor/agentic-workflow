using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The outlooks before the code index is composed: the gate is off, every repo absent.</summary>
public sealed class NoIndexOutlooksTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Lane", "unit")]
    public async Task Every_repo_of_the_hold_is_Absent_so_the_map_publishes_at_once()
    {
        var hold = new MapHoldState(14, _now, new Dictionary<string, MapRepoHead>(StringComparer.Ordinal) { ["a"] = new("x", OnDefaultBranch: true), ["b"] = new("y", OnDefaultBranch: true) });
        var read = await new NoIndexOutlooks().Read(hold, CancellationToken.None);
        Assert.False(read.IsError);
        Assert.All(read.Value.Values, static d => Assert.Equal(IndexOutlook.Absent, d.Outlook));
        Assert.Equal(["a", "b"], read.Value.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var action = MapPublishDecision.Decide(new MapPublishInput(hold, 14, 13, read.Value, TimeSpan.FromMinutes(15)), _now);
        Assert.Equal(MapPublishReason.NoIndex, Assert.IsType<MapPublish>(action).Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task There_are_no_index_commits_to_compare()
    {
        var commits = await new NoIndexOutlooks().IndexCommits(["a", "b"], CancellationToken.None);
        Assert.False(commits.IsError);
        Assert.Empty(commits.Value);
    }
}
