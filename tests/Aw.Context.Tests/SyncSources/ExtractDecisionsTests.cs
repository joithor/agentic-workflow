using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Models;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Fakes;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.Onyx;

namespace Aw.Context.Tests.SyncSources;

/// <summary>
/// Decision extraction, supersedes and intent-vs-intent conflicts inside a sync (S-5B, spec §6.1, §8.8, §8.9): the model behind
/// 'context.labeling', under 'maxTokensPerSync', over the fake Onyx and a scripted model.
/// </summary>
[Trait("Lane", "integration")]
public sealed class ExtractDecisionsTests
{
    private const string R103 = "linear:issue/0e5a1b00-0000-4000-8000-000000000103";
    private const string R104 = "linear:issue/0e5a1b00-0000-4000-8000-000000000104";
    private const string R105 = "linear:issue/0e5a1b00-0000-4000-8000-000000000105";
    private const string Toggle = "surface:command/toggle-orientation";
    private const string Resize = "feature:layout.pane-resize";
    private const string Q103 = "toggling orientation keeps the focused pane first";
    private const string Q104 = "toggle-orientation always resets focus to the first pane";
    private const string QOld = "the minimum pane width is 10 columns";
    private const string QNew = "the minimum pane width is 20 columns";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ContextScope Scope(bool labeling = true, int? perSync = null)
    {
        var cap = perSync is { } n ? ",\"maxTokensPerSync\":" + n.ToString(CultureInfo.InvariantCulture) : string.Empty;
        var section = labeling ? ",\"labeling\":{\"enabled\":true" + cap + "}" : string.Empty;
        return MapWorld.HumanWith("{\"sources\":{\"linear\":{\"teams\":[\"SPL\"]}}" + section + "}");
    }

    private static string Plant(string json) => json
        .Replace("Resize step defaults to 2 columns", "Resize step (layout.pane-resize) defaults to 2 columns", StringComparison.Ordinal)
        .Replace("Restoring a tab brings back orientation and focus", "Restoring a tab brings back orientation and focus; " + Q104, StringComparison.Ordinal);

    private static string Decisions(string quote, string node, string wording, string? replaces = null) =>
        $$"""{"quote":"{{quote}}","wording":"{{wording}}","decides":["{{node}}"]{{(replaces is null ? string.Empty : $",\"replaces\":\"{replaces}\"")}}}""";

    private static string Route(ModelRequest request, string verdict) => request.Purpose switch
    {
        ModelPurpose.DecisionExtraction when request.Prompt.Contains(R103, StringComparison.Ordinal) => "{\"decisions\":[" + Decisions(Q103, Toggle, "Toggling keeps focus first.") + "]}",
        ModelPurpose.DecisionExtraction when request.Prompt.Contains(R104, StringComparison.Ordinal) => "{\"decisions\":[" + Decisions(Q104, Toggle, "Toggling resets focus.") + "]}",
        ModelPurpose.DecisionExtraction when request.Prompt.Contains(R105, StringComparison.Ordinal) =>
            "{\"decisions\":[" + Decisions(QOld, Resize, "Ten columns.") + "," + Decisions(QNew, Resize, "Twenty columns.", QOld) + "]}",
        ModelPurpose.Adjudication => "{\"verdict\":\"" + verdict + "\"}",
        ModelPurpose.DecisionExtraction or ModelPurpose.Labeling or ModelPurpose.Rerank => "{\"decisions\":[]}",
        _ => "{\"decisions\":[]}",
    };

    private sealed class Rig : IAsyncDisposable
    {
        private Rig(FakeOnyxServer onyx, OnyxSourceHarness harness, MapWorld world)
        {
            Onyx = onyx;
            Harness = harness;
            World = world;
        }

        public FakeOnyxServer Onyx { get; }

        public OnyxSourceHarness Harness { get; }

        public MapWorld World { get; }

        public static async Task<Rig> Start(string verdict = "contradicts")
        {
            var onyx = await FakeOnyxServer.Start(new FakeServerOptions { OnyxPlant = Plant });
            var harness = new OnyxSourceHarness(onyx.Url, onyx.Token);
            var world = new MapWorld(intent: harness.Source);
            world.Model.Responder = r => Route(r, verdict);
            _ = await world.ImportFixture();
            return new Rig(onyx, harness, world);
        }

        public async Task<SourceSync> Sync(ContextScope scope) => (await World.Engine.Sync("onyx", scope, Ct)).Value.Sources[0];

        public async Task<IntentLayer> Layer() => (await World.Intents.Layer(Ct)).Value;

        public async ValueTask DisposeAsync()
        {
            World.Dispose();
            Harness.Dispose();
            await Onyx.DisposeAsync();
        }
    }

    [Fact]
    public async Task Extract_stores_model_decisions_with_their_quote_and_a_decides_edge_to_a_linked_node()
    {
        await using var rig = await Rig.Start();

        var row = await rig.Sync(Scope());

        var layer = await rig.Layer();
        var decision = Assert.Single(layer.Decisions, d => d.Quote == Q103);
        Assert.Equal((R103, Provenance.Model), (decision.Record.Value, decision.Provenance));
        Assert.Contains(layer.DecisionEdges, e => e.Type == EdgeType.Decides && e.From.Value == decision.ToKey().Value && e.To.Value == Toggle);
        Assert.Equal(layer.Decisions.Count, row.Decisions);
        Assert.True(row.Tokens > 0);
        Assert.Equal(0, row.Queued);
    }

    [Fact]
    public async Task Extract_stops_at_maxTokensPerSync_and_leaves_the_rest_queued()
    {
        await using var all = await Rig.Start();
        _ = await all.Sync(Scope());
        var linked = all.World.Model.Requests.Count(r => r.Purpose == ModelPurpose.DecisionExtraction);
        await using var capped = await Rig.Start();

        var row = await capped.Sync(Scope(perSync: 1_100));

        var asked = capped.World.Model.Requests.Count(r => r.Purpose == ModelPurpose.DecisionExtraction);
        Assert.InRange(asked, 1, linked - 1);
        Assert.True(row.Tokens <= 1_100);
        Assert.Equal(linked - asked, row.Queued);
        Assert.Equal(ContextErrors.LabelingUnavailable.Code, row.Code);
    }

    [Fact]
    public async Task Extract_with_labeling_off_queues_and_reports_042()
    {
        await using var rig = await Rig.Start();

        var row = await rig.Sync(Scope(labeling: false));

        Assert.Empty(rig.World.Model.Requests);
        Assert.Equal(ContextErrors.LabelingUnavailable.Code, row.Code);
        var layer = await rig.Layer();
        Assert.Empty(layer.Decisions);
        Assert.Equal(layer.Records.Count(r => layer.Links.Any(l => l.Record.Value == r.Key.Value && l.Target.Kind != NodeKind.Record)), row.Queued);   // linked records wait; records no rule linked have nothing to extract
        Assert.True(row.Queued > 0);
    }

    [Fact]
    public async Task Queued_records_are_extracted_by_a_later_sync_once_labeling_is_on()
    {
        await using var rig = await Rig.Start();
        _ = await rig.Sync(Scope(labeling: false));

        var row = await rig.Sync(Scope());

        Assert.Equal((0, null), (row.Queued, row.Code));
        Assert.Contains((await rig.Layer()).Decisions, d => d.Quote == Q103);
    }

    [Fact]
    public async Task Replacing_decision_in_same_record_writes_supersedes()
    {
        await using var rig = await Rig.Start();

        _ = await rig.Sync(Scope());

        var edges = (await rig.Layer()).DecisionEdges;
        var supersedes = Assert.Single(edges, e => e.Type == EdgeType.Supersedes);
        Assert.Equal((DecisionRow.KeyFor(RecordKey.From(R105), QNew).Value, DecisionRow.KeyFor(RecordKey.From(R105), QOld).Value), (supersedes.From.Value, supersedes.To.Value));
    }

    [Fact]
    public async Task Two_current_decisions_on_one_node_that_disagree_are_a_contradicts_conflict()
    {
        await using var rig = await Rig.Start("contradicts");

        _ = await rig.Sync(Scope());

        var conflict = Assert.Single((await rig.Layer()).Conflicts);
        Assert.Equal(ConflictVerdict.Contradicts, conflict.Verdict);
        Assert.Equal(new[] { DecisionRow.KeyFor(RecordKey.From(R103), Q103).Value, DecisionRow.KeyFor(RecordKey.From(R104), Q104).Value }.Order(StringComparer.Ordinal),
            [conflict.A.Value, conflict.B.Value], StringComparer.Ordinal);
    }

    [Fact]
    public async Task Agreeing_decisions_are_stored_as_agrees_and_Conflict_check_is_asked_once_per_pair()
    {
        await using var rig = await Rig.Start("agrees");

        _ = await rig.Sync(Scope());
        _ = await rig.Sync(Scope());

        Assert.Equal(ConflictVerdict.Agrees, Assert.Single((await rig.Layer()).Conflicts).Verdict);
        _ = Assert.Single(rig.World.Model.Requests, r => r.Purpose == ModelPurpose.Adjudication);
    }

    [Fact]
    public async Task A_malformed_model_answer_is_counted_refused_and_keeps_no_decision()
    {
        await using var rig = await Rig.Start();
        rig.World.Model.Responder = _ => "I would rather not.";

        var row = await rig.Sync(Scope());

        Assert.Empty((await rig.Layer()).Decisions);
        Assert.True(row.Refused > 0);
        Assert.Equal(0, row.Queued);   // the version was asked about, so it isn't asked again
    }
}
