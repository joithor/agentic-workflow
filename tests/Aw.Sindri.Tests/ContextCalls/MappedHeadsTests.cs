using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The heads the move check compares with (code index spec 6.4, Supersede): the held draft's, else the current version's, else none.</summary>
public sealed class MappedHeadsTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, string> Heads(params (string Repo, string Sha)[] h) => h.ToDictionary(static x => x.Repo, static x => x.Sha, StringComparer.Ordinal);

    private static MapHoldState Hold(params (string Repo, string Sha)[] h) =>
        new(14, _now, h.ToDictionary(static x => x.Repo, static x => new MapRepoHead(x.Sha, OnDefaultBranch: true), StringComparer.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void The_held_drafts_heads_win_over_the_current_versions() =>
        Assert.Equal(Heads(("a", "new")), MappedHeads.Of(Hold(("a", "new")), Heads(("a", "old"))));

    [Fact, Trait("Lane", "unit")]
    public void A_draft_with_no_hold_does_not_count_so_the_current_versions_heads_are_used() =>
        Assert.Equal(Heads(("a", "old")), MappedHeads.Of(hold: null, Heads(("a", "old"))));

    [Fact, Trait("Lane", "unit")]
    public void No_hold_and_no_current_version_reads_as_none() =>
        Assert.Null(MappedHeads.Of(hold: null, currentHeads: null));

    [Fact, Trait("Lane", "unit")]
    public void A_version_with_no_recorded_heads_reads_as_none()
    {
        Assert.Null(MappedHeads.Of(hold: null, Heads()));
        Assert.Null(MappedHeads.Of(Hold(), Heads(("a", "old"))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_head_off_the_default_branch_is_still_the_commit_the_map_was_built_from() =>
        Assert.Equal(Heads(("a", "x")), MappedHeads.Of(new MapHoldState(14, _now, new Dictionary<string, MapRepoHead>(StringComparer.Ordinal) { ["a"] = new("x", OnDefaultBranch: false) }), currentHeads: null));
}
