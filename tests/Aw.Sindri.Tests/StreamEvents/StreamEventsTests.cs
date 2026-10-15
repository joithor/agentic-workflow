using System.Text.Json.Nodes;
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
        Assert.All(page.Events, e => Assert.Equal(("service.heartbeat", "local", grant.Epoch), (e.Type, e.Tenant, e.Epoch)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_item_created_row_comes_in_its_public_form_with_a_clean_title()
    {
        var h = new SliceHarness();
        var grant = await h.HoldLease();
        var created = new ItemCreated(h.Ids.New<CommandId>(), A.FirstItemId, "Fix \u001b[31mSECRET\u001b[0m", Repo: null, Link: null, Body: "private SECRET body", Owner: null);
        _ = await h.Store.Append(h.Events.Entry(EventTypes.ItemCreated, created, "item.created:x", A.FirstItemId.Value), grant, TestContext.Current.CancellationToken);
        var service = new StreamEventsService(h.Store, h.Events, new MaskScrubber("SECRET"), new InMemoryViewStore(SliceHarness.Json), h.Callers, SliceHarness.Json);

        var row = Assert.Single((await service.Handle(new StreamEventsRequest(After: 0), TestContext.Current.CancellationToken)).Value.Events);

        var payload = JsonNode.Parse(row.Payload)!.AsObject();
        Assert.False(payload.ContainsKey("body"));
        Assert.Equal("Fix \\x1B[31m[masked]\\x1B[0m", payload["title"]!.GetValue<string>());
    }

    private sealed class MaskScrubber(string secret) : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(secret, "[masked]", StringComparison.Ordinal);
    }
}
