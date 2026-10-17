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

    private static WatchSnapshot Res(bool disk = false, bool memory = false) => new([], "m1", disk, memory);

    [Fact, Trait("Lane", "unit")]
    public void Disk_changes_are_reported_once_two_polls_agree() =>
        Assert.Equal(["DISK LOW", "DISK OK"], Run(Snap(), Snap(diskLow: true), Snap(diskLow: true), Snap(diskLow: true), Snap(), Snap(), Snap()), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Memory_changes_are_reported_once_two_polls_agree() =>
        Assert.Equal(["MEMORY LOW", "MEMORY OK"], Run(Snap(), Res(memory: true), Res(memory: true), Res(memory: true), Res(), Res()), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_resource_flapping_around_its_floor_is_never_reported() =>
        Assert.Empty(Run(Snap(), Res(memory: true, disk: true), Res(), Res(memory: true, disk: true), Res(), Res(memory: true), Res()));

    [Fact, Trait("Lane", "unit")]
    public void A_blip_between_two_agreeing_polls_does_not_flip_back()
    {
        // LOW is reported after two polls; a single OK between LOW polls does not report OK, and a second OK in a row does.
        Assert.Equal(
            ["MEMORY LOW", "MEMORY OK"],
            Run(Snap(), Res(memory: true), Res(memory: true), Res(), Res(memory: true), Res(), Res()),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Disk_and_memory_are_reported_independently() =>
        Assert.Equal(
            ["DISK LOW", "MEMORY LOW", "DISK OK"],
            Run(Snap(), Res(disk: true, memory: true), Res(disk: true, memory: true), Res(memory: true), Res(memory: true)),
            StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_state_an_earlier_watch_reported_is_not_announced_again()
    {
        var state = WatchState.Continuing(new WatchResources(DiskLow: true, MemoryLow: true));
        var lines = new List<string>();
        foreach (var poll in new[] { Res(disk: true, memory: true), Res(disk: true, memory: true), Res(disk: true), Res(disk: true) })
        {
            var step = WatchDiff.Step(state, poll);
            lines.AddRange(step.Lines);
            state = step.State;
        }

        Assert.Equal(["MEMORY OK"], lines, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("disk=low mem=ok", true, false)]
    [InlineData("disk=ok mem=low", false, true)]
    [InlineData("disk=low mem=low", true, true)]
    [InlineData("anything else", false, false)]
    public void The_kept_form_reads_back(string text, bool disk, bool memory)
    {
        var read = WatchResources.Parse(text);

        Assert.Equal(new WatchResources(disk, memory), read);
        Assert.Equal(read, WatchResources.Parse(read.ToString()));
        Assert.Equal(WatchResources.None, WatchResources.Parse(null));
    }
}
