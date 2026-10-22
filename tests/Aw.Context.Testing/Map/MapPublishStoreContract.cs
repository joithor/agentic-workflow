using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Kernel;
using Xunit;

namespace Aw.Context.Testing.Map;

/// <summary>The graph store's publish half (retrieval spec §8.7a, code index spec §6.4): the hold, the wait's chain and each version's heads. The fake and the SQLite store pass the same tests.</summary>
public abstract class MapPublishStoreContract
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");
    private static readonly ContextRepoName _other = ContextRepoName.From("other");
    private static readonly MapHead[] _heads = [new(_repo, "1111111", OnDefault: true), new(_other, "2222222222222222222222222222222222222222", OnDefault: false)];

    private static CancellationToken None => TestContext.Current.CancellationToken;

    private static DateTimeOffset T0 => SampleMap.At;

    /// <summary>A map the build wrote: the sample with its heads.</summary>
    /// <param name="suffix">Makes two builds differ.</param>
    /// <param name="heads">The heads, or the two default ones.</param>
    /// <returns>The map.</returns>
    private static MapImport Build(string suffix, params MapHead[] heads) => SampleMap.Build(suffix) with { Heads = heads.Length == 0 ? _heads : heads };

    /// <summary>An empty store holds nothing and reads no heads.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_store_holds_nothing()
    {
        const string test = nameof(An_empty_store_holds_nothing);
        var store = CreateStore();

        var state = Expect.Ok(await store.Held(None), test);

        Expect.That(state is { Hold: null, LatestDraft: null, Current: null, CurrentHeads.Count: 0 }, test, "an empty store reported a hold, a draft, a current version or heads");
        Expect.That(Expect.Ok(await store.Heads(1, None), test).Count == 0, test, "heads of a version that is not there");
        Expect.That(Expect.Ok(await store.Release(1, None), test) == Unit.Value, test, "releasing a version that is not held failed");
    }

    /// <summary>A draft's heads read back as the build named them, in order.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_drafts_heads_read_back_in_the_order_the_build_named_them()
    {
        const string test = nameof(A_drafts_heads_read_back_in_the_order_the_build_named_them);
        var store = CreateStore();

        var draft = Expect.Ok(await store.SaveDraft(Build(string.Empty), T0, None), test);

        Expect.That(Expect.Ok(await store.Heads(draft.Number, None), test).SequenceEqual(_heads), test, "the heads changed on the way through the store");
    }

    /// <summary>An import keeps the one commit it names, as the repo's head on the default branch; one with no commit keeps none.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_import_keeps_the_commit_it_names_and_none_for_a_null_commit()
    {
        const string test = nameof(An_import_keeps_the_commit_it_names_and_none_for_a_null_commit);
        var store = CreateStore();

        var named = Expect.Ok(await store.SaveCurrent(SampleMap.Build(), T0, None), test);
        var bare = Expect.Ok(await store.SaveCurrent(SampleMap.Build() with { Commit = null }, T0.AddDays(1), None), test);

        Expect.That(Expect.Ok(await store.Heads(named.Number, None), test).SequenceEqual([new MapHead(_repo, "a41b07d", OnDefault: true)]), test, "an import did not keep the commit it names");
        Expect.That(Expect.Ok(await store.Heads(bare.Number, None), test).Count == 0, test, "an import with no commit got a head");
    }

    /// <summary>A commit that is not 7 to 40 lowercase hex is refused (191) and nothing is written, by a build, a replacement and an import.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_commit_that_is_not_lowercase_hex_is_refused_and_nothing_is_written()
    {
        const string test = nameof(A_commit_that_is_not_lowercase_hex_is_refused_and_nothing_is_written);
        var store = CreateStore();
        var open = Expect.Ok(await store.SaveDraft(Build(" (open)"), T0, None), test);

        foreach (var bad in new[] { "abc12", "ABCDEF1", "abcdefg", "0a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", string.Empty })
        {
            MapHead[] heads = [new(_repo, bad, OnDefault: true)];
            var saved = await store.SaveDraft(Build(" (bad)", heads), T0.AddDays(1), None);
            var replaced = await store.ReplaceDraft(open.Number, Build(" (bad)", heads), T0.AddDays(1), None);
            var imported = await store.SaveCurrent(Build(" (bad)", heads), T0.AddDays(1), None);

            Expect.That(saved.IsError && saved.Error.Code == "SND-CONTEXT-191", test, $"a draft with commit '{bad}' was not SND-CONTEXT-191");
            Expect.That(replaced.IsError && replaced.Error.Code == "SND-CONTEXT-191", test, $"a replacement with commit '{bad}' was not SND-CONTEXT-191");
            Expect.That(imported.IsError && imported.Error.Code == "SND-CONTEXT-191", test, $"an import with commit '{bad}' was not SND-CONTEXT-191");
        }

        var twice = await store.SaveDraft(Build(" (twice)", new MapHead(_repo, "1111111", OnDefault: true), new MapHead(_repo, "2222222", OnDefault: true)), T0, None);
        Expect.That(twice.IsError && twice.Error.Code == "SND-CONTEXT-191", test, "a repo named twice was not SND-CONTEXT-191");
        var versions = Expect.Ok(await store.Versions(None), test);
        Expect.That(versions.Count == 1 && versions[0].Number == open.Number, test, "a refused write left a version behind");
        Expect.That(Expect.Ok(await store.Heads(open.Number, None), test).SequenceEqual(_heads), test, "a refused replacement changed the open draft's heads");
    }

    /// <summary>A hold stores the draft, reads back with its heads and the time now as the start of the wait, and shows the current version and its heads beside it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_hold_reads_back_with_the_draft_the_current_version_and_their_heads()
    {
        const string test = nameof(A_hold_reads_back_with_the_draft_the_current_version_and_their_heads);
        var store = CreateStore();
        var current = Expect.Ok(await store.SaveCurrent(Build(string.Empty, new MapHead(_repo, "abcdef0", OnDefault: true)), T0, None), test);
        var draft = Expect.Ok(await store.SaveDraft(Build(" (draft)"), T0.AddDays(1), None), test);

        var held = Expect.Ok(await store.Hold(new MapHold(draft.Number, T0.AddDays(2), Heads: []), None), test);

        Expect.That(held.Version == draft.Number && held.Since == T0.AddDays(2) && held.Heads.SequenceEqual(_heads), test, "the stored hold is not the draft, the time now and the draft's heads");
        var state = Expect.Ok(await store.Held(None), test);
        Expect.That(state.Hold is { } stored && stored.Version == held.Version && stored.Since == held.Since && stored.Heads.SequenceEqual(held.Heads) && state.LatestDraft == draft.Number && state.Current == current.Number, test, "the state does not name the hold, the draft and the current version");
        Expect.That(state.CurrentHeads.SequenceEqual([new MapHead(_repo, "abcdef0", OnDefault: true)]), test, "the state's current heads are not the current version's");
    }

    /// <summary>A hold on a version that is not the latest draft (an older draft, the current version, one never written) is 190 and stores nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Holding_a_version_that_is_not_the_latest_draft_is_190_and_stores_nothing()
    {
        const string test = nameof(Holding_a_version_that_is_not_the_latest_draft_is_190_and_stores_nothing);
        var store = CreateStore();
        var current = Expect.Ok(await store.SaveCurrent(Build(string.Empty), T0, None), test);
        var older = Expect.Ok(await store.SaveDraft(Build(" (one)"), T0.AddDays(1), None), test);
        var latest = Expect.Ok(await store.SaveDraft(Build(" (two)"), T0.AddDays(2), None), test);

        foreach (var wrong in new[] { older.Number, current.Number, 999 })
        {
            var refused = await store.Hold(new MapHold(wrong, T0.AddDays(3), []), None);
            Expect.That(refused.IsError && refused.Error.Code == "SND-CONTEXT-190", test, $"holding version {wrong.ToString(CultureInfo.InvariantCulture)} was not SND-CONTEXT-190");
        }

        var state = Expect.Ok(await store.Held(None), test);
        Expect.That(state.Hold is null && state.LatestDraft == latest.Number, test, "a refused hold stored something");
        _ = Expect.Ok(await store.Hold(new MapHold(latest.Number, T0.AddDays(3), []), None), test);
        var noDraft = Expect.Ok(await store.Promote(latest.Number, None), test);
        var afterPromote = await store.Hold(new MapHold(noDraft.Number, T0.AddDays(4), []), None);
        Expect.That(afterPromote.IsError && afterPromote.Error.Code == "SND-CONTEXT-190", test, "holding a promoted version was not SND-CONTEXT-190");
    }

    /// <summary>A hold with an invalid head is 191 and stores nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_hold_with_an_invalid_head_is_191_and_stores_nothing()
    {
        const string test = nameof(A_hold_with_an_invalid_head_is_191_and_stores_nothing);
        var store = CreateStore();
        var draft = Expect.Ok(await store.SaveDraft(Build(string.Empty), T0, None), test);

        var refused = await store.Hold(new MapHold(draft.Number, T0, [new MapHead(_repo, "NOT-HEX", OnDefault: true)]), None);

        Expect.That(refused.IsError && refused.Error.Code == "SND-CONTEXT-191", test, "an invalid head was not SND-CONTEXT-191");
        Expect.That(Expect.Ok(await store.Held(None), test).Hold is null, test, "a refused hold stored something");
    }

    /// <summary>The wait's start is the chain's: a second hold after a rebuild keeps the first one's start, and a start in the future is clamped to now.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_start_of_the_wait_survives_a_rebuild_and_is_clamped_to_now()
    {
        const string test = nameof(The_start_of_the_wait_survives_a_rebuild_and_is_clamped_to_now);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveDraft(Build(" (one)"), T0, None), test);
        _ = Expect.Ok(await store.Hold(new MapHold(first.Number, T0.AddMinutes(1), []), None), test);
        var second = Expect.Ok(await store.SaveDraft(Build(" (two)"), T0.AddMinutes(10), None), test);

        var held = Expect.Ok(await store.Hold(new MapHold(second.Number, T0.AddMinutes(11), []), None), test);

        Expect.That(held.Since == T0.AddMinutes(1), test, "the second hold did not keep the chain's start");
        var third = Expect.Ok(await store.SaveDraft(Build(" (three)"), T0.AddMinutes(20), None), test);
        var early = Expect.Ok(await store.Hold(new MapHold(third.Number, T0.AddSeconds(30), []), None), test);   // the clock stepped back before the chain's start
        Expect.That(early.Since == T0.AddSeconds(30), test, "a start later than now was not clamped to now");
    }

    /// <summary>A build over a held draft drops the hold and keeps the chain.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_build_over_a_held_draft_drops_the_hold_and_keeps_the_chain()
    {
        const string test = nameof(A_build_over_a_held_draft_drops_the_hold_and_keeps_the_chain);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveDraft(Build(" (one)"), T0, None), test);
        _ = Expect.Ok(await store.Hold(new MapHold(first.Number, T0.AddMinutes(1), []), None), test);

        var second = Expect.Ok(await store.SaveDraft(Build(" (two)"), T0.AddMinutes(5), None), test);

        var state = Expect.Ok(await store.Held(None), test);
        Expect.That(state.Hold is null && state.LatestDraft == second.Number, test, "the hold outlived the draft it described");
        Expect.That(Expect.Ok(await store.Heads(first.Number, None), test).Count == 0, test, "the replaced draft's heads are still there");
        var again = Expect.Ok(await store.Hold(new MapHold(second.Number, T0.AddMinutes(9), []), None), test);
        Expect.That(again.Since == T0.AddMinutes(1), test, "the chain's start did not survive the rebuild");
    }

    /// <summary>Any promote clears the hold and the chain and keeps the version's heads; the next hold starts a new wait.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Any_promote_clears_the_hold_and_the_chain_and_keeps_the_heads()
    {
        const string test = nameof(Any_promote_clears_the_hold_and_the_chain_and_keeps_the_heads);
        var store = CreateStore();
        var draft = Expect.Ok(await store.SaveDraft(Build(string.Empty), T0, None), test);
        _ = Expect.Ok(await store.Hold(new MapHold(draft.Number, T0.AddMinutes(1), []), None), test);

        var promoted = Expect.Ok(await store.Promote(draft.Number, None), test);

        var state = Expect.Ok(await store.Held(None), test);
        Expect.That(state.Hold is null && state.LatestDraft is null && state.Current == promoted.Number, test, "the promote left a hold or a draft");
        Expect.That(state.CurrentHeads.SequenceEqual(_heads) && Expect.Ok(await store.Heads(promoted.Number, None), test).SequenceEqual(_heads), test, "the promote lost the heads");
        var next = Expect.Ok(await store.SaveDraft(Build(" (next)"), T0.AddHours(1), None), test);
        var held = Expect.Ok(await store.Hold(new MapHold(next.Number, T0.AddHours(2), []), None), test);
        Expect.That(held.Since == T0.AddHours(2), test, "the promote left the chain: the next wait did not start at its own hold");
    }

    /// <summary>An import is a new current version too: it clears the hold and the chain.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_import_clears_the_hold_and_the_chain()
    {
        const string test = nameof(An_import_clears_the_hold_and_the_chain);
        var store = CreateStore();
        var draft = Expect.Ok(await store.SaveDraft(Build(string.Empty), T0, None), test);
        _ = Expect.Ok(await store.Hold(new MapHold(draft.Number, T0.AddMinutes(1), []), None), test);

        _ = Expect.Ok(await store.SaveCurrent(SampleMap.Build(" (import)"), T0.AddMinutes(2), None), test);

        Expect.That(Expect.Ok(await store.Held(None), test).Hold is null, test, "an import left the hold");
        var held = Expect.Ok(await store.Hold(new MapHold(draft.Number, T0.AddMinutes(30), []), None), test);
        Expect.That(held.Since == T0.AddMinutes(30), test, "an import left the chain");
    }

    /// <summary>A replacement copies the replaced draft's heads, drops its hold, and the promote that follows clears the chain: a renumbering promote keeps the heads.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Replacing_a_draft_copies_its_heads_and_a_promote_after_it_clears_the_wait()
    {
        const string test = nameof(Replacing_a_draft_copies_its_heads_and_a_promote_after_it_clears_the_wait);
        var store = CreateStore();
        var draft = Expect.Ok(await store.SaveDraft(Build(string.Empty), T0, None), test);
        _ = Expect.Ok(await store.Hold(new MapHold(draft.Number, T0.AddMinutes(1), []), None), test);

        var renumbered = Expect.Ok(await store.ReplaceDraft(draft.Number, SampleMap.Build(" (accepted)"), T0.AddMinutes(2), None), test);

        Expect.That(renumbered.Number > draft.Number, test, "the replacement kept the number");
        Expect.That(Expect.Ok(await store.Heads(renumbered.Number, None), test).SequenceEqual(_heads), test, "the replacement did not copy the replaced draft's heads");
        Expect.That(Expect.Ok(await store.Held(None), test).Hold is null, test, "the hold outlived the draft it described");
        var promoted = Expect.Ok(await store.Promote(renumbered.Number, None), test);
        Expect.That(Expect.Ok(await store.Heads(promoted.Number, None), test).SequenceEqual(_heads), test, "the promote of a renumbered draft lost the heads");
    }

    /// <summary>A replacement that names heads keeps those instead of copying.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_replacement_that_names_heads_keeps_those()
    {
        const string test = nameof(A_replacement_that_names_heads_keeps_those);
        var store = CreateStore();
        var draft = Expect.Ok(await store.SaveDraft(Build(string.Empty), T0, None), test);
        MapHead[] named = [new(_repo, "3333333", OnDefault: false)];

        var replaced = Expect.Ok(await store.ReplaceDraft(draft.Number, Build(" (named)", named), T0.AddMinutes(2), None), test);

        Expect.That(Expect.Ok(await store.Heads(replaced.Number, None), test).SequenceEqual(named), test, "the replacement's own heads were not kept");
    }

    /// <summary>Opening a draft copies the current version's heads.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Opening_a_draft_copies_the_current_versions_heads()
    {
        const string test = nameof(Opening_a_draft_copies_the_current_versions_heads);
        var store = CreateStore();
        _ = Expect.Ok(await store.SaveCurrent(Build(string.Empty), T0, None), test);

        var opened = Expect.Ok(await store.OpenDraft(T0.AddDays(1), None), test);

        Expect.That(Expect.Ok(await store.Heads(opened.Number, None), test).SequenceEqual(_heads), test, "the opened draft did not copy the current version's heads");
    }

    /// <summary>A retained version keeps its heads; deleting it takes them with it, and a deleted draft takes its hold.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_retained_version_keeps_its_heads_until_it_is_deleted()
    {
        const string test = nameof(A_retained_version_keeps_its_heads_until_it_is_deleted);
        var store = CreateStore();
        var first = Expect.Ok(await store.SaveCurrent(Build(string.Empty), T0, None), test);
        _ = Expect.Ok(await store.SaveCurrent(Build(" (2)", new MapHead(_repo, "4444444", OnDefault: true)), T0.AddDays(1), None), test);

        Expect.That(Expect.Ok(await store.Heads(first.Number, None), test).SequenceEqual(_heads), test, "a retained version lost its heads");
        _ = Expect.Ok(await store.Delete([first.Number], None), test);

        Expect.That(Expect.Ok(await store.Heads(first.Number, None), test).Count == 0, test, "a deleted version's heads are still there");
    }

    /// <summary>Releasing drops the hold and leaves the draft and the chain.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Releasing_drops_the_hold_and_keeps_the_draft_and_the_chain()
    {
        const string test = nameof(Releasing_drops_the_hold_and_keeps_the_draft_and_the_chain);
        var store = CreateStore();
        var draft = Expect.Ok(await store.SaveDraft(Build(string.Empty), T0, None), test);
        _ = Expect.Ok(await store.Hold(new MapHold(draft.Number, T0.AddMinutes(1), []), None), test);

        _ = Expect.Ok(await store.Release(draft.Number, None), test);

        var state = Expect.Ok(await store.Held(None), test);
        Expect.That(state.Hold is null && state.LatestDraft == draft.Number, test, "the release removed the draft or kept the hold");
        var again = Expect.Ok(await store.Hold(new MapHold(draft.Number, T0.AddMinutes(9), []), None), test);
        Expect.That(again.Since == T0.AddMinutes(1), test, "the release cleared the chain");
        _ = Expect.Ok(await store.Release(draft.Number + 5, None), test);
        Expect.That(Expect.Ok(await store.Held(None), test).Hold is not null, test, "releasing another version dropped the hold");
    }

    /// <summary>Overrides are appended only while the expected draft is still the latest (190 otherwise, also with no draft), and without an expectation as before.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Overrides_with_an_expected_draft_are_appended_only_while_it_is_the_latest()
    {
        const string test = nameof(Overrides_with_an_expected_draft_are_appended_only_while_it_is_the_latest);
        var store = CreateStore();
        var old = Expect.Ok(await store.SaveDraft(Build(" (one)"), T0, None), test);
        var latest = Expect.Ok(await store.SaveDraft(Build(" (two)"), T0.AddDays(1), None), test);
        var row = new MapOverride(OverrideAction.Accept, NodeKey.From("feature:layout.split-pane"), "service:engine", T0.AddDays(2));
        var rows = Expect.Ok(await store.Overrides(None), test).Count;

        var stale = await store.AppendOverrides([row], None, expectedDraft: old.Number);

        Expect.That(stale.IsError && stale.Error.Code == "SND-CONTEXT-190", test, "rows for an older draft were not SND-CONTEXT-190");
        Expect.That(Expect.Ok(await store.Overrides(None), test).Count == rows, test, "a refused append added rows");
        Expect.That(Expect.Ok(await store.AppendOverrides([row], None, expectedDraft: latest.Number), test) == 1, test, "rows for the latest draft were not appended");
        var promoted = Expect.Ok(await store.Promote(latest.Number, None), test);
        var none = await store.AppendOverrides([row with { At = T0.AddDays(3) }], None, expectedDraft: promoted.Number);
        Expect.That(none.IsError && none.Error.Code == "SND-CONTEXT-190", test, "rows for a promoted draft were not SND-CONTEXT-190");
        Expect.That(Expect.Ok(await store.AppendOverrides([row with { At = T0.AddDays(3) }], None), test) == 1, test, "an append with no expectation was refused");
    }

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IGraphStore CreateStore();
}
