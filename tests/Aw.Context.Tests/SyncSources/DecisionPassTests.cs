using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Linking;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

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
        var pass = new DecisionPass(store, model, new LabelingSettings { Enabled = true, MaxTokensPerSync = 2_000 }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run(records, Ct);

        var queued = (await store.Queued(_onyx, Ct)).Value;
        Assert.Equal(1, pass.Refused);   // the oversized record, counted
        Assert.False(pass.HeldBack);   // it can never fit, so it neither waits nor stops the pass
        Assert.Empty(queued);
        Assert.Contains(_small.Value, model.Requests.Select(r => r.Prompt).Single(), StringComparison.Ordinal);   // the small one was still asked
        _ = Assert.Single(model.Requests);
    }

    private static readonly NodeKey _node = NodeKey.From("feature:a");

    private static async Task Commit(FakeIntentStore store, params PendingRecord[] records) =>
        _ = await store.CommitPage(new IntentPage(_onyx, [.. records.Select(r => r.Record)], [.. records.SelectMany(r => r.Links)], Cursor: null, _at), Ct);

    /// <summary>A model that answers like the fake but reports <paramref name="spent"/> input tokens for every call.</summary>
    private sealed class CostlyModel(FakeModelPort inner, int spent) : IModelPort
    {
        public async ValueTask<Result<ModelAnswer>> Ask(ModelRequest request, CancellationToken cancellationToken)
        {
            var answer = await inner.Ask(request, cancellationToken);
            return answer.IsError ? answer : new ModelAnswer(answer.Value.Text, spent, 0);
        }
    }

    [Fact]
    public async Task A_record_whose_extraction_leaves_no_room_for_its_conflict_checks_gives_up_the_pairs_and_does_not_block_the_records_after_it()
    {
        const string quote = "Toggling keeps focus first";
        var store = new FakeIntentStore();
        var earlier = Pending(_small, "Another decision on the node");
        var first = Pending(_big, quote + ".");
        var later = Pending(RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000203"), "A later record");
        await Commit(store, earlier);
        var seeded = new DecisionRow(_small, "Another decision on the node", "Other", Provenance.Model, _at);
        _ = await store.SaveDecisions(new DecisionBatch(_small, "v1", [seeded], [new GraphEdge(seeded.ToKey(), _node, EdgeType.Decides, Provenance.Model, 1.0, At: _at)]), Ct);
        await Commit(store, first, later);
        var fake = new FakeModelPort { Answer = $$"""{"decisions":[{"quote":"{{quote}}","wording":"Keeps focus","decides":["feature:a"]}]}""" };
        var cap = 2_000;
        var pass = new DecisionPass(store, new CostlyModel(fake, cap - 20), new LabelingSettings { Enabled = true, MaxTokensPerSync = cap }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run([first, later], Ct);

        var queued = (await store.Queued(_onyx, Ct)).Value.Select(k => k.Value);
        var layer = (await store.Layer(Ct)).Value;
        Assert.DoesNotContain(_big.Value, queued, StringComparer.Ordinal);   // done: its checks can never fit, so they are given up, not retried forever ahead of the records after it
        Assert.Contains(later.Record.Key.Value, queued, StringComparer.Ordinal);   // held back this run, and now first in line
        Assert.Equal(ConflictVerdict.Refused, Assert.Single(layer.Conflicts).Verdict);
        Assert.Equal(1, pass.Refused);
        _ = Assert.Single(layer.Decisions, d => d.Record == _big);
    }

    [Fact]
    public async Task A_store_error_while_queuing_a_held_back_record_stops_the_pass_with_its_code()
    {
        var store = new FakeIntentStore { RequeueFailure = ContextErrors.StoreUnavailable.With("requeue failed") };
        var record = Pending(_big, "text");
        await Commit(store, record);
        var pass = new DecisionPass(store, new FakeModelPort(), new LabelingSettings { Enabled = false }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run([record], Ct);

        Assert.Equal((true, ContextErrors.StoreUnavailable.Code), (pass.HeldBack, pass.Failure));
    }

    [Fact]
    public async Task A_store_error_while_giving_up_an_oversized_record_stops_the_pass_with_its_code()
    {
        var store = new FakeIntentStore { EmptyBatchFailure = ContextErrors.StoreUnavailable.With("save failed") };
        var model = new FakeModelPort { Answer = """{"decisions":[]}""" };
        var records = new[] { Pending(_big, new string('x', 12_000)), Pending(_small, "short") };
        await Commit(store, records);
        var pass = new DecisionPass(store, model, new LabelingSettings { Enabled = true, MaxTokensPerSync = 2_000 }, (new FakeScrubber(), new FakeTimeProvider(_at)));

        await pass.Run(records, Ct);

        Assert.Equal(ContextErrors.StoreUnavailable.Code, pass.Failure);
        Assert.Empty(model.Requests);   // the pass stopped: the record after it was held back, not asked
    }
}
