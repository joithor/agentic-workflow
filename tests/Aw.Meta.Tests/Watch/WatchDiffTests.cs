using Aw.Meta.Core.Watch;

namespace Aw.Meta.Tests.Watch;

public sealed class WatchDiffTests
{
    private static PrView Pr(int number = 5, PrState state = PrState.Open, bool draft = false, string head = "aaaaaaa1", string mergeable = "MERGEABLE") =>
        new(number, state, draft, head, mergeable);

    private static WatchSnapshot Snap(string main = "m1", bool diskLow = false, params PrView[] prs) => new(prs, main, diskLow);

    private static List<string> Run(params WatchSnapshot[] polls)
    {
        var state = WatchState.Initial;
        var lines = new List<string>();
        foreach (var poll in polls)
        {
            var step = WatchDiff.Step(state, poll);
            lines.AddRange(step.Lines);
            state = step.State;
        }

        return lines;
    }

    [Fact, Trait("Lane", "unit")]
    public void The_first_poll_sets_the_baseline_silently() =>
        Assert.Empty(Run(Snap(prs: Pr())));

    [Fact, Trait("Lane", "unit")]
    public void A_new_pull_request_is_reported_once() =>
        Assert.Equal(["PR #6 opened"], Run(Snap(prs: Pr()), Snap(prs: [Pr(), Pr(6)]), Snap(prs: [Pr(), Pr(6)])), StringComparer.Ordinal);

    [Theory, Trait("Lane", "unit")]
    [InlineData(PrState.Merged, "PR #5 merged")]
    [InlineData(PrState.Closed, "PR #5 closed")]
    public void A_pull_request_that_leaves_open_is_reported(PrState to, string line) =>
        Assert.Equal([line], Run(Snap(prs: Pr()), Snap(prs: Pr(state: to)), Snap(prs: Pr(state: to))), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Draft_and_ready_flips_are_reported() =>
        Assert.Equal(
            ["PR #5 ready", "PR #5 draft"],
            Run(Snap(prs: Pr(draft: true)), Snap(prs: Pr()), Snap(prs: Pr(draft: true))),
            StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_moved_head_is_reported_with_its_short_sha() =>
        Assert.Equal(["PR #5 head bbbbbbb"], Run(Snap(prs: Pr()), Snap(prs: Pr(head: "bbbbbbb9999"))), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_mergeable_flip_is_reported_only_after_it_holds_two_polls() =>
        Assert.Equal(
            ["PR #5 mergeable CONFLICTING"],
            Run(Snap(prs: Pr()), Snap(prs: Pr(mergeable: "CONFLICTING")), Snap(prs: Pr(mergeable: "CONFLICTING")), Snap(prs: Pr(mergeable: "CONFLICTING"))),
            StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_flip_that_does_not_hold_is_never_reported() =>
        Assert.Empty(Run(Snap(prs: Pr()), Snap(prs: Pr(mergeable: "CONFLICTING")), Snap(prs: Pr()), Snap(prs: Pr(mergeable: "CONFLICTING")), Snap(prs: Pr())));

    [Fact, Trait("Lane", "unit")]
    public void UNKNOWN_is_never_reported_and_does_not_break_a_pending_flip() =>
        Assert.Equal(
            ["PR #5 mergeable CONFLICTING"],
            Run(
                Snap(prs: Pr(mergeable: "UNKNOWN")),
                Snap(prs: Pr(mergeable: "UNKNOWN")),
                Snap(prs: Pr(mergeable: "MERGEABLE")),
                Snap(prs: Pr(mergeable: "CONFLICTING")),
                Snap(prs: Pr(mergeable: "UNKNOWN")),
                Snap(prs: Pr(mergeable: "CONFLICTING"))),
            StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Main_moving_is_reported_once_per_move() =>
        Assert.Equal(["MAIN moved bbbbbbb"], Run(Snap(), Snap("bbbbbbb1234"), Snap("bbbbbbb1234")), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Disk_changes_are_reported_when_they_change() =>
        Assert.Equal(["DISK LOW", "DISK OK"], Run(Snap(), Snap(diskLow: true), Snap(diskLow: true), Snap()), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Memory_changes_are_reported_when_they_change() =>
        Assert.Equal(
            ["MEMORY LOW", "MEMORY OK"],
            Run(Snap(), new WatchSnapshot([], "m1", DiskLow: false, MemoryLow: true), new WatchSnapshot([], "m1", DiskLow: false, MemoryLow: true), Snap()),
            StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Disk_and_memory_are_reported_independently() =>
        Assert.Equal(
            ["DISK LOW", "MEMORY LOW", "DISK OK"],
            Run(Snap(), new WatchSnapshot([], "m1", DiskLow: true, MemoryLow: true), new WatchSnapshot([], "m1", DiskLow: false, MemoryLow: true)),
            StringComparer.Ordinal);
}
