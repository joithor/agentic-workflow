using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Linking;

namespace Aw.Context.Tests.SyncSources;

/// <summary>The decision pass on its own: what it does with a record whose extraction can never fit 'maxTokensPerSync'.</summary>
[Trait("Lane", "unit")]
public sealed class DecisionPassTests
{
    private static readonly SourceId _onyx = SourceId.From("onyx");
    private static readonly RecordKey _big = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000201");
    private static readonly RecordKey _small = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000202");
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-08-01T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PendingRecord Pending(RecordKey key, string text) =>
        new(new IntentRecord(key, _onyx, "v1", "Title", []), text, [new IntentLink(key, NodeKey.From("feature:a"), LinkRule.IssueKey, 1.0, "ab12")]);

    [Fact]
    public async Task A_record_whose_prompt_alone_exceeds_the_cap_is_refused_and_not_queued_and_the_rest_is_still_extracted()
    {
        var store = new FakeIntentStore();
        var model = new FakeModelPort { Answer = """{"decisions":[]}""" };
        var records = new[] { Pending(_big, new string('x', 12_000)), Pending(_small, "short") };
        _ = await store.CommitPage(new IntentPage(_onyx, [.. records.Select(r => r.Record)], [.. records.SelectMany(r => r.Links)], Cursor: null, _at), Ct);
        var pass = new DecisionPass(store, model, new LabelingSettings { Enabled = true, MaxTokensPerSync = 2_000 }, (new FakeScrubber(), TimeProvider.System));

        await pass.Run(records, Ct);

        var queued = (await store.Queued(_onyx, Ct)).Value;
        Assert.Equal(1, pass.Refused);   // the oversized record, counted
        Assert.False(pass.HeldBack);   // it can never fit, so it neither waits nor stops the pass
        Assert.Empty(queued);
        Assert.Contains(_small.Value, model.Requests.Select(r => r.Prompt).Single(), StringComparison.Ordinal);   // the small one was still asked
        _ = Assert.Single(model.Requests);
    }
}
