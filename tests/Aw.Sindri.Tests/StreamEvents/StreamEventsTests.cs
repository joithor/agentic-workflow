using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.StreamEvents;

public sealed class StreamEventsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task StreamEvents_returns_the_events_after_the_cursor_with_their_envelope()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var record = new RecordHeartbeatService(h.Store, h.Events, h.Clock);
        for (var i = 0; i < 3; i++)
        {
            _ = await record.Handle(new RecordHeartbeatRequest(new ScheduledRun(grant, h.Clock.GetUtcNow().AddSeconds(i))), TestContext.Current.CancellationToken);
        }

        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 1), TestContext.Current.CancellationToken)).Value;

        Assert.Equal([2L, 3L], page.Events.Select(e => e.Sequence));
        Assert.All(page.Events, e => Assert.Equal(("service.heartbeat", "local"), (e.Type, e.Tenant)));
        Assert.Equal(3L, page.ScannedTo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_item_created_row_comes_in_its_public_form_with_a_clean_title()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var created = new ItemCreated(h.Ids.New<CommandId>(), A.FirstItemId, "Fix \u001b[31mSECRET\u001b[0m", Repo: null, Link: null, Body: "private SECRET body", Owner: null);
        _ = await h.Store.Append(h.Events.Entry(EventTypes.ItemCreated, created, "item.created:x", A.FirstItemId.Value), grant, TestContext.Current.CancellationToken);
        var service = StreamEventsServices.Over(h, new MaskScrubber("SECRET"));

        var row = Assert.Single((await service.Handle(new StreamEventsRequest(After: 0), TestContext.Current.CancellationToken)).Value.Events);

        Assert.False(row.Payload.TryGetProperty("body", out _));
        Assert.Equal("Fix \\x1B[31m[masked]\\x1B[0m", row.Payload.GetProperty("title").GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_is_scoped_by_the_ledgers_item_repo_with_the_views_empty()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var ct = TestContext.Current.CancellationToken;
        var (mine, other) = (A.FirstItemId, ItemId.From("ITM-00000000000000000000000000000002"));
        foreach (var (item, repo) in new[] { (mine, "demo"), (other, "elsewhere") })
        {
            var created = new ItemCreated(h.Ids.New<CommandId>(), item, "t", RepoName.From(repo), Link: null, Body: null, Owner: null);
            _ = await h.Store.Append(h.Events.Entry(EventTypes.ItemCreated, created, "item.created:" + item.Value, item.Value), grant, ct);
            var moved = new ItemStateChanged(h.Ids.New<CommandId>(), item, ItemState.New, ItemState.Queued, 2, Reason: null);
            _ = await h.Store.Append(h.Events.Entry(EventTypes.ItemStateChanged, moved, "item.state-changed:" + item.Value + ":v2", item.Value), grant, ct);
        }

        // the items view holds nothing, as during a rebuild: the scope still comes from the ledger
        var session = new CallerContext(CallerClass.Session, Terminal: false, "session:cli", h.Ids.New<CommandId>(), h.Ids.New<CorrelationId>(), RepoName.From("demo"));
        using var entered = h.Callers.Enter(session);
        var page = (await StreamEventsServices.Over(h).Handle(new StreamEventsRequest(After: 0), ct)).Value;

        Assert.Equal([1L, 2L], page.Events.Select(e => e.Sequence));
        Assert.All(page.Events, e => Assert.Equal(mine, e.Item));
        Assert.Equal(4L, page.ScannedTo);
    }

    private sealed class MaskScrubber(string secret) : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(secret, "[masked]", StringComparison.Ordinal);
    }
}
