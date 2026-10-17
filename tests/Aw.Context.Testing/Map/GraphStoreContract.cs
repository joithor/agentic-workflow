using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Sources;
using Xunit;

namespace Aw.Context.Testing.Map;

/// <summary>The graph store's contract (spec §6.2): the fake and the SQLite store pass the same tests.</summary>
public abstract class GraphStoreContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>An empty store has no current version.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_store_has_no_map()
    {
        const string test = nameof(An_empty_store_has_no_map);
        var store = CreateStore();

        Expect.That(Expect.Ok(await store.Current(None), test) is null, test, "a current version in an empty store");
        Expect.That(Expect.Ok(await store.Versions(None), test).Count == 0, test, "versions in an empty store");
        Expect.That(Expect.Ok(await store.Overrides(None), test).Count == 0, test, "overrides in an empty store");
    }

    /// <summary>A saved map reads back as written: evidence, aliases, decisions, attributes, timestamps and provenance.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_saved_map_is_current_and_reads_back_as_written()
    {
        const string test = nameof(A_saved_map_is_current_and_reads_back_as_written);
        var store = CreateStore();
        var map = SampleMap.Build();

        var saved = Expect.Ok(await store.SaveCurrent(map, SampleMap.At, None), test);
        var read = Expect.Ok(await store.Read(saved.Number, None), test);

        Expect.That(Expect.Ok(await store.Current(None), test)?.Number == saved.Number, test, "the saved version isn't current");
        Expect.That(saved.Repo == map.Repo && saved.Commit == map.Commit, test, "repo or commit changed");
        Expect.That(Canonical.Of(map.Graph) == Canonical.Of(read), test, $"read back differently:\n{Canonical.Of(read)}\nexpected:\n{Canonical.Of(map.Graph)}");
        Expect.That(Expect.Ok(await store.Overrides(None), test).SequenceEqual(map.Overrides), test, "overrides changed");
    }

    /// <summary>A second save retires the first; overrides are kept once.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_second_save_retains_the_first_and_keeps_overrides_once()
    {
        const string test = nameof(A_second_save_retains_the_first_and_keeps_overrides_once);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);

        var second = Expect.Ok(await store.SaveCurrent(SampleMap.Build(" (2)"), SampleMap.At.AddDays(1), None), test);

        var versions = Expect.Ok(await store.Versions(None), test);
        Expect.That(versions.Select(v => (v.Number, v.Status)).SequenceEqual([(second.Number, MapVersionStatus.Current), (first.Number, MapVersionStatus.Retained)]),
            test, "expected the second current and the first retained, newest first");
        Expect.That(Expect.Ok(await store.Overrides(None), test).Count == 1, test, "the same override was kept twice");
        Expect.That(Canonical.Of(Expect.Ok(await store.Read(first.Number, None), test)) == Canonical.Of(SampleMap.Build().Graph), test, "the retained version changed");
    }

    /// <summary>Delete removes retained versions only.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Delete_removes_retained_versions_only()
    {
        const string test = nameof(Delete_removes_retained_versions_only);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var second = Expect.Ok(await store.SaveCurrent(SampleMap.Build(" (2)"), SampleMap.At, None), test);

        var deleted = Expect.Ok(await store.Delete([first.Number, second.Number], None), test);

        Expect.That(deleted == 1, test, $"deleted {deleted.ToString(System.Globalization.CultureInfo.InvariantCulture)}, expected only the retained one");
        Expect.That(Expect.Ok(await store.Versions(None), test).Select(v => v.Number).SequenceEqual([second.Number]), test, "the current version went too");
        Expect.That(Expect.Ok(await store.Read(first.Number, None), test).Nodes.Count == 0, test, "a deleted version's graph is still there");
    }

    /// <summary>A snapshot is the current version with its graph and overrides; an empty store and a store with no draft have none.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_snapshot_is_the_current_version_with_its_graph_and_overrides()
    {
        const string test = nameof(A_snapshot_is_the_current_version_with_its_graph_and_overrides);
        var store = CreateStore();
        Expect.That(Expect.Ok(await store.Snapshot(draft: false, None), test) is null, test, "a snapshot in an empty store");
        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var second = Expect.Ok(await store.SaveCurrent(SampleMap.Build(" (2)"), SampleMap.At.AddDays(1), None), test);

        var snapshot = Expect.Ok(await store.Snapshot(draft: false, None), test);

        Expect.That(snapshot?.Version.Number == second.Number, test, "the snapshot isn't the current version");
        Expect.That(snapshot is not null && Canonical.Of(snapshot.Graph) == Canonical.Of(SampleMap.Build(" (2)").Graph), test, "the snapshot's graph differs");
        Expect.That(snapshot?.Overrides.Count == 1, test, "the snapshot's overrides differ");
        Expect.That(Expect.Ok(await store.Snapshot(draft: true, None), test) is null, test, "a draft snapshot with no draft");
    }

    /// <summary>A retained version reads with its own graph and today's whole override layer, in one session (scoping spec C10).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task SnapshotAt_reads_a_retained_version_with_todays_overrides_in_one_session()
    {
        const string test = nameof(SnapshotAt_reads_a_retained_version_with_todays_overrides_in_one_session);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var second = Expect.Ok(await store.SaveCurrent(SampleMap.Build(" (2)"), SampleMap.At.AddDays(1), None), test);
        var later = new MapOverride(OverrideAction.Accept, NodeKey.From("feature:layout.split-pane"), "avery", SampleMap.At.AddDays(2));
        _ = Expect.Ok(await store.AppendOverrides([later], None), test);

        var old = Expect.Ok(await store.SnapshotAt(first.Number, None), test);
        var current = Expect.Ok(await store.SnapshotAt(second.Number, None), test);

        Expect.That(old is not null && old.Version.Status == MapVersionStatus.Retained && old.Version.Number == first.Number, test, "the retained version isn't the one asked for");
        Expect.That(old is not null && Canonical.Of(old.Graph) == Canonical.Of(SampleMap.Build().Graph), test, "the retained version's graph differs");
        Expect.That(old is not null && old.Overrides.Any(o => o.ToId() == later.ToId()), test, "a later override is missing from the old version's read");
        Expect.That(current is not null && current.Version.Status == MapVersionStatus.Current && Canonical.Of(current.Graph) == Canonical.Of(SampleMap.Build(" (2)").Graph), test, "the current version doesn't read by number");
    }

    /// <summary>A draft, a version never written and one already deleted are not held: only 'current' and 'retained' versions read by number.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task SnapshotAt_a_draft_an_unwritten_or_a_deleted_version_is_not_held()
    {
        const string test = nameof(SnapshotAt_a_draft_an_unwritten_or_a_deleted_version_is_not_held);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build(" (2)"), SampleMap.At.AddDays(1), None), test);
        var draft = Expect.Ok(await store.SaveDraft(SampleMap.Build(" (draft)"), SampleMap.At.AddDays(2), None), test);
        _ = Expect.Ok(await store.Delete([first.Number], None), test);

        Expect.That(Expect.Ok(await store.SnapshotAt(draft.Number, None), test) is null, test, "a draft read by number");
        Expect.That(Expect.Ok(await store.SnapshotAt(999, None), test) is null, test, "a version never written");
        Expect.That(Expect.Ok(await store.SnapshotAt(first.Number, None), test) is null, test, "a deleted version");
    }

    /// <summary>A draft is a new version beside the current one: current stays current and the draft reads back as written.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_draft_leaves_the_current_version_untouched()
    {
        const string test = nameof(A_draft_leaves_the_current_version_untouched);
        var store = CreateStore();
        var current = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);

        var draft = Expect.Ok(await store.SaveDraft(SampleMap.Build(" (draft)"), SampleMap.At.AddDays(1), None), test);

        Expect.That(draft.Status == MapVersionStatus.Draft && draft.Number > current.Number, test, "the saved version isn't a newer draft");
        Expect.That(Expect.Ok(await store.Current(None), test)?.Number == current.Number, test, "the draft replaced the current version");
        Expect.That(Expect.Ok(await store.Versions(None), test).Select(v => (v.Number, v.Status)).SequenceEqual([(draft.Number, MapVersionStatus.Draft), (current.Number, MapVersionStatus.Current)]),
            test, "expected the draft then the current version, newest first");
        var snapshot = Expect.Ok(await store.Snapshot(draft: true, None), test);
        Expect.That(snapshot?.Version.Number == draft.Number && Canonical.Of(snapshot.Graph) == Canonical.Of(SampleMap.Build(" (draft)").Graph), test, "the draft snapshot differs");
        Expect.That(Canonical.Of(Expect.Ok(await store.Read(current.Number, None), test)) == Canonical.Of(SampleMap.Build().Graph), test, "the current graph changed");
    }

    /// <summary>A new draft replaces the open one (its undecided items go with it); every override stays, once.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_new_draft_replaces_the_open_one_and_keeps_every_override()
    {
        const string test = nameof(A_new_draft_replaces_the_open_one_and_keeps_every_override);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveDraft(SampleMap.Build(" (one)"), SampleMap.At, None), test);
        var decided = new MapOverride(OverrideAction.Accept, NodeKey.From("feature:layout.split-pane"), "avery", SampleMap.At.AddHours(1));
        _ = Expect.Ok(await store.AppendOverrides([decided], None), test);

        var second = Expect.Ok(await store.SaveDraft(SampleMap.Build(" (two)"), SampleMap.At.AddDays(1), None), test);

        var drafts = Expect.Ok(await store.Versions(None), test).Where(v => v.Status == MapVersionStatus.Draft).ToList();
        Expect.That(drafts.Count == 1 && drafts[0].Number == second.Number, test, "expected exactly the second draft");
        Expect.That(Expect.Ok(await store.Read(first.Number, None), test).Nodes.Count == 0, test, "the replaced draft's graph is still there");
        var overrides = Expect.Ok(await store.Overrides(None), test);
        Expect.That(overrides.Count == 2 && overrides.Any(o => o.ToId() == decided.ToId()), test, "a decision was lost or an override kept twice");
    }

    /// <summary>Replacing the open draft swaps it for the new graph, keeps every override once and leaves current alone.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Replacing_the_open_draft_swaps_it_and_keeps_the_current_version()
    {
        const string test = nameof(Replacing_the_open_draft_swaps_it_and_keeps_the_current_version);
        var store = CreateStore();
        var current = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var open = Expect.Ok(await store.SaveDraft(SampleMap.Build(" (one)"), SampleMap.At.AddDays(1), None), test);

        var decision = new MapOverride(OverrideAction.Undo, NodeKey.From("feature:a"), "avery", SampleMap.At.AddDays(2), Undoes: OverrideId.From("ovr_0123456789abcdef"));
        var two = SampleMap.Build(" (two)");

        var replaced = Expect.Ok(await store.ReplaceDraft(open.Number, two with { Overrides = [decision] }, SampleMap.At.AddDays(2), None), test);
        Expect.That(Expect.Ok(await store.Overrides(None), test).Where(o => o.ToId() == decision.ToId()).Take(2).Count() == 1, test, "the replacement's own write didn't store the override");
        _ = Expect.Ok(await store.AppendOverrides([decision], None), test);

        Expect.That(replaced.Status == MapVersionStatus.Draft && replaced.Number > open.Number, test, "the replacement isn't a newer draft");
        Expect.That(Expect.Ok(await store.Versions(None), test).Select(v => (v.Number, v.Status)).SequenceEqual([(replaced.Number, MapVersionStatus.Draft), (current.Number, MapVersionStatus.Current)]),
            test, "expected the new draft and the current version only");
        Expect.That(Canonical.Of(Expect.Ok(await store.Snapshot(draft: true, None), test)!.Graph) == Canonical.Of(two.Graph), test, "the draft snapshot isn't the replacement");
        Expect.That(Expect.Ok(await store.Overrides(None), test).Where(o => o.ToId() == decision.ToId()).Take(2).Count() == 1, test, "the replacement's override wasn't kept exactly once");
    }

    /// <summary>A draft that another caller replaced or promoted since it was read is not replaced again: SND-CONTEXT-046, and the newer one stays.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Replacing_a_draft_that_changed_since_it_was_read_is_046_and_changes_nothing()
    {
        const string test = nameof(Replacing_a_draft_that_changed_since_it_was_read_is_046_and_changes_nothing);
        var store = CreateStore();
        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var read = Expect.Ok(await store.SaveDraft(SampleMap.Build(" (read)"), SampleMap.At.AddDays(1), None), test);
        var rebuilt = Expect.Ok(await store.SaveDraft(SampleMap.Build(" (rebuilt)"), SampleMap.At.AddDays(2), None), test);

        var stale = await store.ReplaceDraft(read.Number, SampleMap.Build(" (cut)"), SampleMap.At.AddDays(3), None);

        Expect.That(stale.IsError && stale.Error.Code == "SND-CONTEXT-046", test, "replacing a draft that was replaced since wasn't SND-CONTEXT-046");
        Expect.That(stale.Error.Message.Contains("no longer the open draft", StringComparison.Ordinal), test, "the refusal doesn't say the draft is no longer the open one");
        var drafts = Expect.Ok(await store.Versions(None), test).Where(v => v.Status == MapVersionStatus.Draft).ToList();
        Expect.That(drafts.Count == 1 && drafts[0].Number == rebuilt.Number, test, "the newer draft didn't stay the only one");
        Expect.That(Canonical.Of(Expect.Ok(await store.Read(rebuilt.Number, None), test)) == Canonical.Of(SampleMap.Build(" (rebuilt)").Graph), test, "the newer draft's graph changed");
        var promoted = Expect.Ok(await store.Promote(rebuilt.Number, None), test);
        var afterPromote = await store.ReplaceDraft(promoted.Number, SampleMap.Build(" (late)"), SampleMap.At.AddDays(4), None);
        Expect.That(afterPromote.IsError && afterPromote.Error.Code == "SND-CONTEXT-046", test, "replacing a promoted draft wasn't SND-CONTEXT-046");
        Expect.That(!Expect.Ok(await store.Versions(None), test).Any(v => v.Status == MapVersionStatus.Draft), test, "a draft appeared");
    }

    /// <summary>A failed or empty store gives no draft to open until there is a current version to copy.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Opening_a_draft_without_a_current_version_is_no_map()
    {
        const string test = nameof(Opening_a_draft_without_a_current_version_is_no_map);
        var store = CreateStore();

        var opened = await store.OpenDraft(SampleMap.At, None);

        Expect.That(opened.IsError && opened.Error.Code == "SND-CONTEXT-030", test, "expected SND-CONTEXT-030");
        Expect.That(opened.Error.Message.Contains("context map import <file>", StringComparison.Ordinal) && !opened.Error.Message.Contains("map build", StringComparison.Ordinal), test, "the fix line must name a command that works today");
        Expect.That(Expect.Ok(await store.Versions(None), test).Count == 0, test, "a version appeared");
    }

    /// <summary>Opening a draft copies the current version once; asking again returns the open one.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Opening_a_draft_copies_the_current_version_once()
    {
        const string test = nameof(Opening_a_draft_copies_the_current_version_once);
        var store = CreateStore();
        var current = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);

        var draft = Expect.Ok(await store.OpenDraft(SampleMap.At.AddDays(1), None), test);
        var again = Expect.Ok(await store.OpenDraft(SampleMap.At.AddDays(2), None), test);

        Expect.That(draft.Status == MapVersionStatus.Draft && draft.Repo == current.Repo && draft.Commit == current.Commit, test, "the copy isn't a draft of the same repo and commit");
        Expect.That(again.Number == draft.Number, test, "a second open made another draft");
        Expect.That(Canonical.Of(Expect.Ok(await store.Read(draft.Number, None), test)) == Canonical.Of(SampleMap.Build().Graph), test, "the copy differs from the current graph");
        Expect.That(Expect.Ok(await store.Current(None), test)?.Number == current.Number, test, "current changed");
        Expect.That(Expect.Ok(await store.Versions(None), test).Count == 2, test, "expected the current version and one draft");
    }

    /// <summary>Promote makes the draft current and the old current retained; only a draft can be promoted.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Promote_makes_the_draft_current_and_retains_the_old_one()
    {
        const string test = nameof(Promote_makes_the_draft_current_and_retains_the_old_one);
        var store = CreateStore();
        var current = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), SampleMap.At, None), test);
        var draft = Expect.Ok(await store.SaveDraft(SampleMap.Build(" (draft)"), SampleMap.At.AddDays(1), None), test);

        var notDraft = await store.Promote(current.Number, None);
        var unknown = await store.Promote(999, None);
        var promoted = Expect.Ok(await store.Promote(draft.Number, None), test);

        Expect.That(notDraft.IsError && notDraft.Error.Code == "SND-CONTEXT-046", test, "promoting the current version wasn't SND-CONTEXT-046");
        Expect.That(unknown.IsError && unknown.Error.Code == "SND-CONTEXT-046", test, "promoting an unknown version wasn't SND-CONTEXT-046");
        Expect.That(promoted.Number == draft.Number && promoted.Status == MapVersionStatus.Current, test, "the draft isn't current");
        Expect.That(Expect.Ok(await store.Versions(None), test).Select(v => (v.Number, v.Status)).SequenceEqual([(draft.Number, MapVersionStatus.Current), (current.Number, MapVersionStatus.Retained)]),
            test, "expected the draft current and the old one retained");
        Expect.That(Expect.Ok(await store.Snapshot(draft: true, None), test) is null, test, "a draft is still open after the promote");
        Expect.That(Canonical.Of(Expect.Ok(await store.Read(draft.Number, None), test)) == Canonical.Of(SampleMap.Build(" (draft)").Graph), test, "the promoted graph changed");
    }

    /// <summary>Appended overrides are one row each, kept once, and read back with their link, undo and proposal fields.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Appended_overrides_read_back_whole_and_a_repeat_adds_nothing()
    {
        const string test = nameof(Appended_overrides_read_back_whole_and_a_repeat_adds_nothing);
        var store = CreateStore();
        var at = SampleMap.At.AddHours(2);
        var proposal = new MapOverride(OverrideAction.Propose, NodeKey.From("feature:tabs.groups"), "service:engine", at, "Tab groups", Proposal: new ProposalPayload(
            "run-7", ContextRepoName.From("aw-split-panes"), NodeKind.Feature, NodeKey.From("feature:tabs.tab-manager"),
            [new EvidenceRef(RecordKey.From("repo:aw-split-panes/docs/architecture.md"), "L1-L4")], "tab groups"));
        var link = new MapOverride(OverrideAction.Accept, NodeKey.From("code:src/tabs/TabManager.ts"), "avery", at.AddMinutes(1),
            Link: LinkId.From("edge:0123456789abcdef"));
        var undo = new MapOverride(OverrideAction.Undo, link.Target, "avery", at.AddMinutes(2), Undoes: link.ToId());

        var added = Expect.Ok(await store.AppendOverrides([proposal, link, undo], None), test);
        var repeated = Expect.Ok(await store.AppendOverrides([link, undo], None), test);

        Expect.That(added == 3 && repeated == 0, test, $"added {added.ToString(System.Globalization.CultureInfo.InvariantCulture)} then {repeated.ToString(System.Globalization.CultureInfo.InvariantCulture)}, expected 3 then 0");
        var read = Expect.Ok(await store.Overrides(None), test);
        Expect.That(read.Select(o => o.ToId().Value).SequenceEqual([proposal.ToId().Value, link.ToId().Value, undo.ToId().Value], StringComparer.Ordinal), test, "the overrides or their order changed");
        var got = read.Single(o => o.Action == OverrideAction.Propose);
        Expect.That(string.Equals(got.Label, "Tab groups", StringComparison.Ordinal) && got.Proposal is { Kind: NodeKind.Feature } p && string.Equals(p.Run, "run-7", StringComparison.Ordinal)
            && p.Parent == proposal.Proposal!.Parent && string.Equals(p.Quote, "tab groups", StringComparison.Ordinal)
            && p.Evidence!.Select(e => (e.Key.Value, e.Span)).SequenceEqual([("repo:aw-split-panes/docs/architecture.md", "L1-L4")]), test, "the proposal payload changed");
        Expect.That(read.Single(o => o.Action == OverrideAction.Accept).Link == link.Link && read.Single(o => o.Action == OverrideAction.Undo).Undoes == link.ToId(), test, "the link or undo fields changed");
    }

    /// <summary>A re-proposal's generation reads back (it is part of the row's id: a store that lost it would mint a different id for the row it wrote).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_re_proposed_rows_generation_reads_back_and_keeps_its_id()
    {
        const string test = nameof(A_re_proposed_rows_generation_reads_back_and_keeps_its_id);
        var store = CreateStore();
        var at = SampleMap.At.AddHours(2);
        var first = new MapOverride(OverrideAction.Propose, NodeKey.From("feature:tabs.groups"), "service:engine", at, "Tab groups", Proposal: new ProposalPayload(
            "run-7", ContextRepoName.From("aw-split-panes"), NodeKind.Feature));
        var again = first with { Proposal = first.Proposal! with { Generation = 2 } };

        _ = Expect.Ok(await store.AppendOverrides([first, again], None), test);

        var read = Expect.Ok(await store.Overrides(None), test);
        Expect.That(read.Count == 2 && first.ToId() != again.ToId(), test, "the first proposal and its re-proposal must be two rows with different ids");
        Expect.That(read.Select(o => (o.ToId().Value, o.Proposal!.Generation)).Order().SequenceEqual(new[] { (first.ToId().Value, 0), (again.ToId().Value, 2) }.Order()), test, "a generation or an id changed on the way through the store");
    }

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IGraphStore CreateStore();
}
