using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The reader behind 'context map status', the Health card and doctor: it matches the ledger's events to the map it reports by repo and version (code index spec 6.4).</summary>
public sealed class MapPublishStatusReaderTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly ContextRepoName _a = ContextRepoName.From("aw-a");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LedgerEvent Event(long sequence, string type, string payload, string actor = "service:context-map-refresh") =>
        new(sequence, Epoch: 1, new EventEnvelope<string>(
            new Guid((int)sequence, 0, 0, new byte[8]), type, SchemaVersion: 1, _at.AddMinutes(sequence), actor, Tenant: "t", Ring: 0, new Guid((int)sequence, 1, 0, new byte[8]), CausationId: null, TraceId: null, payload),
            string.Create(CultureInfo.InvariantCulture, $"k{sequence}"), Item: null);

    private static async Task<MapPublishStatusView> Read(params LedgerEvent[] events)
    {
        var engine = PublishStatusEngineProxy.Create(new MapPublishView([_a], Hold: null, LatestDraft: null, Current: 14, [new MapHead(_a, "1234567", OnDefault: true)]));
        var reader = new MapPublishStatusReader(engine, new NoIndexOutlooks(), new ListLedger(events), new FakeTimeProvider(_at.AddHours(1)));
        var scope = new ContextScope(Settings: null, [], new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Cli));
        var status = await reader.Read(scope, Ct);
        return status.Value;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_repos_sharing_a_version_number_do_not_lend_each_other_their_publish_event()
    {
        var view = await Read(
            Event(1, EventTypes.ContextMapPublished, """{"repo":"aw-a","version":14,"reason":"IndexReady"}"""),
            Event(2, EventTypes.ContextMapPublished, """{"repo":"aw-b","version":14,"reason":"WaitOff"}"""),
            Event(3, EventTypes.ContextMapPromoted, """{"repo":"aw-b","version":14}""", actor: "human:owner"));

        Assert.Equal(("published", "IndexReady", _at.AddMinutes(1)), (view.State, view.Reason, view.PublishedAt));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_hold_dropped_as_the_newest_map_event_says_the_held_map_was_replaced()
    {
        var view = await Read(
            Event(1, EventTypes.ContextMapPublished, """{"repo":"aw-a","version":14,"reason":"IndexReady"}"""),
            Event(2, EventTypes.ContextMapHoldDropped, """{"version":15,"reason":"Replaced"}"""));

        Assert.Equal("Held map v15 was replaced by a newer build.", view.Lines[0]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_publish_after_the_drop_clears_the_replaced_line()
    {
        var view = await Read(
            Event(1, EventTypes.ContextMapHoldDropped, """{"version":15,"reason":"Replaced"}"""),
            Event(2, EventTypes.ContextMapPublished, """{"repo":"aw-a","version":14,"reason":"IndexReady"}"""));

        Assert.DoesNotContain("replaced", view.Lines[0], StringComparison.Ordinal);
    }
}
