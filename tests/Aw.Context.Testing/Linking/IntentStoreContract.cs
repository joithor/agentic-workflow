using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Linking;

/// <summary>The intent store's contract (spec §6.2, S-5B): the fake and the SQLite store pass the same tests.</summary>
public abstract class IntentStoreContract
{
    private static readonly SourceId _onyx = SourceId.From("onyx");
    private static readonly RecordKey _r103 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
    private static readonly RecordKey _r104 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000104");
    private static readonly DateTimeOffset _at = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>Creates a fresh, empty store.</summary>
    /// <returns>The store under test.</returns>
    protected abstract IIntentStore CreateStore();

    private static IntentRecord Record(RecordKey key, string version, string alias) => new(key, _onyx, version, "Title of " + alias, [alias]);

    private static IntentLink Link(RecordKey record, string target, LinkRule rule = LinkRule.IssueKey) => new(record, NodeKey.From(target), rule, 1.0, "ab12");

    private static IntentPage Page(string? cursor, IReadOnlyList<IntentRecord> records, IReadOnlyList<IntentLink> links) => new(_onyx, records, links, cursor, _at);

    /// <summary>An empty store has an empty layer and a zero state.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_store_has_an_empty_layer()
    {
        const string test = nameof(An_empty_store_has_an_empty_layer);
        var store = CreateStore();

        var layer = Expect.Ok(await store.Layer(None), test);
        var state = Expect.Ok(await store.State(_onyx, None), test);

        Expect.That(layer.Records.Count == 0 && layer.Links.Count == 0 && layer.Decisions.Count == 0, test, "rows in an empty store");
        Expect.That(state == new IntentSourceState(Cursor: null, LastSync: null, LastError: null, Records: 0, Links: 0, Decisions: 0, Queued: 0), test, $"state {state}");
    }

    /// <summary>A committed page reads back as written, with its cursor and counts.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_committed_page_reads_back_and_moves_the_cursor()
    {
        const string test = nameof(A_committed_page_reads_back_and_moves_the_cursor);
        var store = CreateStore();
        var record = Record(_r103, "v1", "SPL-103");
        var link = Link(_r103, "feature:layout.split-pane.orientation");

        var commit = Expect.Ok(await store.CommitPage(Page("{\"nodeOffset\":4}", [record], [link]), None), test);
        var layer = Expect.Ok(await store.Layer(None), test);
        var state = Expect.Ok(await store.State(_onyx, None), test);

        Expect.That(commit == new IntentCommit(1, 1, 1), test, $"commit {commit}");
        Expect.That(layer.Records.Count == 1 && layer.Records[0] is var got && got.Key == record.Key && got.Version == record.Version && got.Title == record.Title && got.Aliases.SequenceEqual(record.Aliases, StringComparer.Ordinal) && layer.Links.SequenceEqual([link]), test, "records or links read back differently");
        Expect.That(state.Cursor == "{\"nodeOffset\":4}" && state.LastSync == _at && state.LastError is null && state is { Records: 1, Links: 1, Queued: 1 }, test, $"state {state}");
    }

    /// <summary>Replaying a page writes no new rows and reports nothing changed.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Replaying_a_page_writes_no_new_rows()
    {
        const string test = nameof(Replaying_a_page_writes_no_new_rows);
        var store = CreateStore();
        var page = Page("c1", [Record(_r103, "v1", "SPL-103"), Record(_r104, "v1", "SPL-104")], [Link(_r103, "feature:a"), Link(_r104, "feature:b", LinkRule.Path)]);
        _ = Expect.Ok(await store.CommitPage(page, None), test);
        var before = Expect.Ok(await store.Layer(None), test);

        var replay = Expect.Ok(await store.CommitPage(page, None), test);
        var after = Expect.Ok(await store.Layer(None), test);

        Expect.That(replay.Changed == 0, test, "replay changed " + replay.Changed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Expect.That(before.Records.Count == after.Records.Count && before.Links.Count == after.Links.Count && before.Links.SequenceEqual(after.Links), test, "the replay wrote rows");
    }

    /// <summary>A new version of a record replaces its links; another record's links stay.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_new_version_replaces_that_records_links_only()
    {
        const string test = nameof(A_new_version_replaces_that_records_links_only);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103"), Record(_r104, "v1", "SPL-104")], [Link(_r103, "feature:old"), Link(_r104, "feature:b")]), None), test);

        var next = Expect.Ok(await store.CommitPage(Page("c2", [Record(_r103, "v2", "SPL-103")], [Link(_r103, "feature:new")]), None), test);
        var layer = Expect.Ok(await store.Layer(None), test);

        Expect.That(next.Changed == 1, test, "changed " + next.Changed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Expect.That(layer.Links.Select(l => l.Target.Value).Order(StringComparer.Ordinal).SequenceEqual(["feature:b", "feature:new"], StringComparer.Ordinal), test, "links: " + string.Join(',', layer.Links.Select(l => l.Target.Value)));
        Expect.That(layer.Records.Single(r => r.Key.Value == _r103.Value).Version == "v2", test, "the version didn't move");
    }

    /// <summary>A page that fails changes nothing, cursor included (the links and the cursor commit together).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failing_page_leaves_rows_and_cursor_unchanged()
    {
        const string test = nameof(A_failing_page_leaves_rows_and_cursor_unchanged);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], [Link(_r103, "feature:a")]), None), test);

        var broken = await store.CommitPage(Page("c2", [Record(_r104, "v1", "SPL-104")], [Link(_r103, "feature:z"), Link(RecordKey.From("linear:issue/unknown"), "feature:y")]), None);
        var layer = Expect.Ok(await store.Layer(None), test);
        var state = Expect.Ok(await store.State(_onyx, None), test);

        Expect.That(broken.IsError && broken.Error.Code == ContextErrors.StoreUnavailable.Code, test, "a link to an unknown record was accepted");
        Expect.That(state.Cursor == "c1" && layer.Records.Count == 1 && layer.Links.Count == 1, test, $"state {state}, {layer.Records.Count} records");
    }

    /// <summary>A failed run is remembered with its code and keeps the cursor and last sync.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failed_run_keeps_the_cursor_and_records_its_code()
    {
        const string test = nameof(A_failed_run_keeps_the_cursor_and_records_its_code);
        var store = CreateStore();
        _ = Expect.Ok(await store.CommitPage(Page("c1", [Record(_r103, "v1", "SPL-103")], []), None), test);

        _ = Expect.Ok(await store.RecordFailure(_onyx, "SND-CONTEXT-010", None), test);
        var state = Expect.Ok(await store.State(_onyx, None), test);

        Expect.That(state.Cursor == "c1" && state.LastSync == _at && state.LastError == "SND-CONTEXT-010", test, $"state {state}");
    }
}
