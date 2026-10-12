using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IViewStore"/> (with its <see cref="IViewReader"/>) honours (API spec §2.3): a page
/// folds each event through the projections that handle it, a failing event stops the page there and keeps the events
/// before it, a reset empties the views and the cursor, and rows round-trip through JSON.
/// </summary>
public abstract class ViewStoreContract
{
    private const string Put = "fixture.put";
    private const string Fail = "fixture.fail";
    private const string Throw = "fixture.throw";
    private readonly SequentialIdGenerator _ids = new();

    /// <summary>The contract's clock.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A store and its reader over a new, empty file or memory.</summary>
    /// <returns>The store and the reader.</returns>
    protected abstract (IViewStore Store, IViewReader Reader) Create();

    /// <summary>The first Open creates the store (Fresh, cursor 0); the next one finds it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Open_on_a_new_file_is_fresh()
    {
        var (store, reader) = Create();
        Expect.True((await reader.State(Token)).Value is null);

        var first = (await store.Open(ViewSchema.Version, Token)).Value;
        var second = (await store.Open(ViewSchema.Version, Token)).Value;

        Expect.Equal(new ProjectorState(0, ViewSchema.Version, ProjectedAt: null, Rebuilding: false, StalledAt: null, StalledType: null, Fresh: true), first);
        Expect.True(!second.Fresh);
        Expect.Equal(0L, (await reader.State(Token)).Value?.Cursor);
    }

    /// <summary>Each event goes through every projection that handles it; the cursor and ProjectedAt move to the page's end.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Apply_folds_each_event_through_the_projections_that_handle_it()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);
        var at = Clock.GetUtcNow();

        var applied = (await store.Apply([Event(1, Put, "alpha"), Event(2, "other", "x"), Event(3, Put, "beta")], Projections(), at, Token)).Value;

        Expect.True(applied.Stall is null);
        Expect.Equal(3L, applied.State.Cursor);
        Expect.Equal(at, applied.State.ProjectedAt);
        Expect.Sequence(["alpha", "beta"], (await reader.List<ServiceRow>(ViewNames.Services, Token)).Value.Select(r => r.Name.Value));
        Expect.Sequence(["alpha", "beta"], (await reader.List<ServiceRow>(ViewNames.Profile, Token)).Value.Select(r => r.Name.Value));
        Expect.Equal(3L, (await reader.State(Token)).Value?.Cursor);
    }

    /// <summary>A failing event stops the page: the events before it stay, its own writes and the later events don't, and the stall is recorded.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failing_event_stops_the_page_there_and_keeps_the_events_before_it()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);

        var applied = (await store.Apply([Event(1, Put, "alpha"), Event(2, Fail, "bad"), Event(3, Put, "beta")], Projections(), Clock.GetUtcNow(), Token)).Value;

        Expect.Equal(new ViewStall(2, Fail, FixtureProjection.Failure.Code, FixtureProjection.Failure.Message), applied.Stall);
        var state = (await reader.State(Token)).Value;
        Expect.Equal(1L, state?.Cursor);
        Expect.Equal(2L, state?.StalledAt);
        Expect.Equal(Fail, state?.StalledType);
        Expect.Sequence(["alpha"], (await reader.List<ServiceRow>(ViewNames.Services, Token)).Value.Select(r => r.Name.Value));
    }

    /// <summary>A reset empties the named views, sets the cursor to 0, clears the stall and marks rebuilding until FinishRebuild.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Reset_empties_the_views_and_the_cursor()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);
        _ = await store.Apply([Event(1, Put, "alpha"), Event(2, Fail, "bad")], Projections(), Clock.GetUtcNow(), Token);

        var reset = (await store.Reset(ViewNames.All, ViewSchema.Version + 1, Clock.GetUtcNow(), Token)).Value;

        Expect.True(reset is { Cursor: 0, Rebuilding: true, StalledAt: null, StalledType: null, SchemaVersion: ViewSchema.Version + 1 });
        Expect.Equal(0, (await reader.List<ServiceRow>(ViewNames.Services, Token)).Value.Count);
        Expect.Equal(0, (await reader.List<ServiceRow>(ViewNames.Profile, Token)).Value.Count);
        _ = (await store.FinishRebuild(Token)).Value;
        Expect.True((await reader.State(Token)).Value is { Rebuilding: false, Cursor: 0 });
    }

    /// <summary>A row reads back equal to what the projection wrote, nested records, enums and times included.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Rows_round_trip_through_json()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);
        var recorded = Event(1, Put, "alpha");

        _ = await store.Apply([recorded], Projections(), Clock.GetUtcNow(), Token);

        Expect.Equal(FixtureProjection.RowFor(recorded), (await reader.Get<ServiceRow>(ViewNames.Services, "alpha", Token)).Value);
        Expect.True((await reader.Get<ServiceRow>(ViewNames.Services, "missing", Token)).Value is null);
    }

    /// <summary>A projection that throws stalls the page like one that returns an error (API spec §2.3).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_throwing_projection_stalls_like_a_failing_one()
    {
        var (store, reader) = Create();
        _ = await store.Open(ViewSchema.Version, Token);

        var applied = (await store.Apply([Event(1, Put, "alpha"), Event(2, Throw, "boom")], [.. Projections(), new Throwing()], Clock.GetUtcNow(), Token)).Value;

        Expect.Equal(2L, applied.Stall?.Sequence);
        Expect.Equal(SindriErrors.ViewsStalled.Code, applied.Stall?.Code);
        Expect.True((await reader.State(Token)).Value is { Cursor: 1, StalledAt: 2 });
    }

    private static IReadOnlyList<IProjection> Projections() =>
        [new FixtureProjection(ViewNames.Services, Put), new FixtureProjection(ViewNames.Profile, Put), new FixtureProjection(ViewNames.Services, Fail, fails: true)];

    private LedgerEvent Event(long sequence, string type, string key)
    {
        var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId()).Value;
        Clock.Advance(TimeSpan.FromMilliseconds(1));
        return new LedgerEvent(sequence, 1, context.Envelope(type, """{"n":1}""", Clock, _ids), key, Item: null);
    }

    private sealed class Throwing : IProjection
    {
        public IReadOnlyList<string> Views => [ViewNames.Services];

        public bool Handles(string eventType) => string.Equals(eventType, Throw, StringComparison.Ordinal);

        public ValueTask<Kernel.Result<Kernel.Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("fixture: the fold threw");
    }
}
