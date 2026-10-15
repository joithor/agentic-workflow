using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
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

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IProposalRuns CreateRuns();
}
