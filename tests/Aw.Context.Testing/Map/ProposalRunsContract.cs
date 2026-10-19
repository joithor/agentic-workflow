using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Sources;
using Xunit;

namespace Aw.Context.Testing.Map;

/// <summary>The scoping runs' store contract (spec §8.7): the fake and the SQLite store pass the same tests.</summary>
public abstract class ProposalRunsContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>A run's surfaces read back whole and in order; a second save of the run replaces them; an unknown run reads as null.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_saved_run_reads_back_and_a_second_save_replaces_it()
    {
        const string test = nameof(A_saved_run_reads_back_and_a_second_save_replaces_it);
        var store = CreateRuns();
        var repo = ContextRepoName.From("aw-split-panes");
        var first = new ScopeProposal("run-7", repo,
        [
            new ProposedSurface("Tab manager", NodeKey.From("feature:tabs.tab-manager")),
            new ProposedSurface("Zoom", Key: null, NodeKind.Feature, NodeKey.From("feature:layout"), [new EvidenceRef(RecordKey.From("linear:issue/ENG-1"), "L2")], "zoom in"),
            new ProposedSurface("Something vague"),
        ]);

        var saved = Expect.Ok(await store.SaveRun(first, None), test);
        var run = Expect.Ok(await store.Run("run-7", None), test);
        var read = run?.Surfaces;
        _ = Expect.Ok(await store.SaveRun(first with { Surfaces = [first.Surfaces[2]] }, None), test);
        var replaced = Expect.Ok(await store.Run("run-7", None), test)?.Surfaces;

        Expect.That(saved == 3, test, "the saved count is wrong");
        Expect.That(read is not null && read.Select(s => s.Name).SequenceEqual(["Tab manager", "Zoom", "Something vague"], StringComparer.Ordinal), test, "the surfaces or their order changed");
        Expect.That(read![1].Kind == NodeKind.Feature && read[1].Parent == first.Surfaces[1].Parent && string.Equals(read[1].Quote, "zoom in", StringComparison.Ordinal)
            && read[1].Evidence!.Select(e => (e.Key.Value, e.Span)).SequenceEqual([("linear:issue/ENG-1", "L2")]), test, "a surface's fields changed");
        Expect.That(replaced is { Count: 1 } && replaced[0].Name == "Something vague", test, "the second save didn't replace the first");
        Expect.That(Expect.Ok(await store.Run("run-8", None), test) is null, test, "an unknown run read as something");
        Expect.That(run!.Repo == repo, test, "the run lost its repo");
    }

    /// <summary>Propose saves the run's whole list with the rows it is given; a repeat of the same rows adds none and keeps the list.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Propose_saves_the_run_with_its_rows_and_a_repeat_adds_none()
    {
        const string test = nameof(Propose_saves_the_run_with_its_rows_and_a_repeat_adds_none);
        var store = CreateRuns();
        var repo = ContextRepoName.From("aw-split-panes");
        var run = new ScopeProposal("run-9", repo, [new ProposedSurface("Zoom", Key: null, NodeKind.Feature), new ProposedSurface("Something vague")]);
        var row = new MapOverride(OverrideAction.Propose, NodeKey.From("feature:zoom"), "service:engine", DateTimeOffset.UnixEpoch, "Zoom",
            Proposal: new ProposalPayload("run-9", repo, NodeKind.Feature));

        var first = Expect.Ok(await store.Propose(run, [row], None), test);
        var again = Expect.Ok(await store.Propose(run, [row], None), test);
        var saved = Expect.Ok(await store.Run("run-9", None), test);

        Expect.That(first == 1 && again == 0, test, "the rows added are wrong");
        Expect.That(saved is { Surfaces.Count: 2 }, test, "the run's list wasn't saved with its rows");
    }

    /// <summary>A withdrawal (an empty surface list with the undo rows) writes the rows and leaves the run with an empty list, not none; a repeat adds no row.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_withdrawal_writes_its_undo_rows_and_leaves_the_run_with_an_empty_list()
    {
        const string test = nameof(A_withdrawal_writes_its_undo_rows_and_leaves_the_run_with_an_empty_list);
        var store = CreateRuns();
        var repo = ContextRepoName.From("aw-split-panes");
        var run = new ScopeProposal("run-11", repo, [new ProposedSurface("Zoom", Key: null, NodeKind.Feature)]);
        var row = new MapOverride(OverrideAction.Propose, NodeKey.From("feature:zoom"), "service:engine", DateTimeOffset.UnixEpoch, "Zoom",
            Proposal: new ProposalPayload("run-11", repo, NodeKind.Feature));
        var undo = new MapOverride(OverrideAction.Undo, row.Target, "service:engine", DateTimeOffset.UnixEpoch.AddMinutes(1)) with { Undoes = row.ToId() };
        _ = Expect.Ok(await store.Propose(run, [row], None), test);

        var withdrawn = Expect.Ok(await store.Propose(run with { Surfaces = [] }, [undo], None), test);
        var repeat = Expect.Ok(await store.Propose(run with { Surfaces = [] }, [undo], None), test);
        var standing = Expect.Ok(await store.Run("run-11", None), test);

        Expect.That(withdrawn == 1 && repeat == 0, test, "the undo row wasn't written once");
        Expect.That(standing is { Surfaces.Count: 0 }, test, "the run wasn't left with an empty surface list");
    }

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IProposalRuns CreateRuns();
}
