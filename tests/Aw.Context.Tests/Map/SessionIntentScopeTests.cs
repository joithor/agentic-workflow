using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

/// <summary>Owner decision 2026-10-15: B. A session sees the records and decisions linked to its in-scope nodes (spec §3), fenced and scrubbed, and nothing linked only to nodes outside its repo.</summary>
public sealed class SessionIntentScopeTests : IDisposable
{
    private const string InKey = "feature:in";
    private const string OutKey = "feature:out";
    private const string OutOnlyTitle = "OUTONLY-TITLE-ZEBRA";
    private const string QuoteIn = "keep the pane in view";
    private const string QuoteOut = "OUTONLY-QUOTE-ZEBRA";
    private const string RecordInDecision = "linear:issue/0e5a1b00-0000-4000-8000-000000000a01";
    private const string RecordOutDecision = "linear:issue/0e5a1b00-0000-4000-8000-000000000b01";
    private const string AliasIn = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000a09";
    private const string AliasOut = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000b09";
    private const string PrivateDoc = "repo:aw-split-panes/docs/private/notes.md";
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static string DecisionKey(string record, string quote) => DecisionRow.KeyFor(RecordKey.From(record), quote).Value;

    private static ContextScope SessionDenying(params string[] globs)
    {
        using var section = System.Text.Json.JsonDocument.Parse("""{"sessions":{"enabled":true}}""");
        return new(section.RootElement.Clone(), [new ContextRepo(ContextRepoName.From(FixtureMap.Repo), "/work/aw-split-panes") { DenyPaths = [.. PathDenylist.Defaults, .. globs] }],
            new ContextCaller(ContextCallerKind.Session, ContextRepoName.From(FixtureMap.Repo), ContextClient.Http));
    }

    private async Task ImportMap(string extraQuote = QuoteIn)
    {
        var inDecision = DecisionKey(RecordInDecision, extraQuote);
        var outDecision = DecisionKey(RecordOutDecision, QuoteOut);
        var privateDecision = DecisionKey(PrivateDoc, "private quote");
        _ = await _world.ImportYaml(MapWorld.Yaml(
            "schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:",
            $"  - {{key: \"{InKey}\", label: \"In scope\"}}",
            $"  - {{key: \"{OutKey}\", label: \"Out of scope\"}}",
            "  - {key: \"code:src/in.ts\", label: \"in.ts\", provenance: deterministic}",
            $"  - {{key: \"record:{RecordInDecision}\", label: \"Quoted record\"}}",
            "  - {key: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02\", label: \"Mentions in\"}",
            $"  - {{key: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000b02\", label: \"{OutOnlyTitle}\"}}",
            $"  - {{key: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000c02\", label: \"Both {FakeScrubber.Secret}\"}}",
            "edges:",
            "  - {from: \"code:src/in.ts\", to: \"feature:in\", type: implements, provenance: deterministic, evidence: [{key: \"repo:aw-split-panes/src/in.ts\"}]}",
            "  - {from: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02\", to: \"feature:in\", type: mentions}",
            "  - {from: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000b02\", to: \"feature:out\", type: mentions}",
            "  - {from: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000c02\", to: \"feature:in\", type: mentions}",
            "  - {from: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000c02\", to: \"feature:out\", type: mentions}",
            $"  - {{from: \"record:{RecordInDecision}\", to: \"feature:out\", type: mentions}}",
            $"  - {{from: \"{inDecision}\", to: \"feature:in\", type: decides}}",
            $"  - {{from: \"{outDecision}\", to: \"feature:out\", type: decides}}",
            $"  - {{from: \"{privateDecision}\", to: \"feature:in\", type: decides}}",
            "aliases:",
            $"  - {{alias: \"{AliasIn}\", target: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02\"}}",
            $"  - {{alias: \"{AliasOut}\", target: \"record:linear:issue/0e5a1b00-0000-4000-8000-000000000b02\"}}",
            "decisions:",
            $"  - {{record: \"{RecordInDecision}\", quote: \"{extraQuote}\", wording: \"Keep the pane in view {FakeScrubber.Secret}.\", at: 2026-07-15T10:00:00Z}}",
            $"  - {{record: \"{RecordOutDecision}\", quote: \"{QuoteOut}\", wording: \"{OutOnlyTitle} decision.\", at: 2026-07-15T10:00:00Z}}",
            $"  - {{record: \"{PrivateDoc}\", quote: \"private quote\", wording: \"Private wording.\", at: 2026-07-15T10:00:00Z}}"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_s_intent_band_lists_the_records_and_decisions_linked_to_its_nodes_as_a_human_sees_them()
    {
        await ImportMap();

        var session = (await _world.Engine.Lenses(InKey, read: MapRead.Current, depth: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value.Bands.Intent;
        var human = (await _world.Engine.Lenses(InKey, read: MapRead.Current, depth: null, MapWorld.Human, MapWorld.None)).Value.Bands.Intent;

        Assert.Equal((human.Counts.Records, human.Counts.Decisions), (session.Counts.Records, session.Counts.Decisions));
        Assert.Equal((2, 2), (session.Counts.Records, session.Counts.Decisions));
        Assert.Contains(session.Decisions, d => d.Node.Value == DecisionKey(RecordInDecision, QuoteIn));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_reads_a_record_s_mentions_only_among_its_own_nodes_and_the_quote_fenced_and_scrubbed()
    {
        await ImportMap();
        const string both = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000c02";

        var session = (await _world.Engine.ShowNode(both, MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value;
        var human = (await _world.Engine.ShowNode(both, MapWorld.Human, MapWorld.None)).Value;
        var band = (await _world.Engine.Lenses(InKey, read: MapRead.Current, depth: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value.Bands.Intent;

        Assert.Equal([InKey], session.Out.Select(e => e.To.Value), StringComparer.Ordinal);   // why: the out-of-scope end of the link is not the session's to see
        Assert.Equal([InKey, OutKey], human.Out.Select(e => e.To.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.DoesNotContain(FakeScrubber.Secret, session.Node.Label, StringComparison.Ordinal);
        var decision = Assert.Single(band.Decisions, d => d.Node.Value == DecisionKey(RecordInDecision, QuoteIn));
        Assert.DoesNotContain(FakeScrubber.Secret, decision.Text, StringComparison.Ordinal);
        Assert.Equal(("<untrusted source=\"map\">" + QuoteIn + "</untrusted>", Trust.Untrusted), (decision.Quote!.Text, decision.Quote.Trust));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_title_is_inside_the_fence_for_a_session_in_show_and_neighbors_and_untrusted_for_a_human()
    {
        await ImportMap();
        const string mentions = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02";

        var shown = (await _world.Engine.ShowNode(mentions, MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value;
        var hood = (await _world.Engine.Neighbors(InKey, depth: 1, MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value;
        var human = (await _world.Engine.ShowNode(mentions, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(("<untrusted source=\"map\">Mentions in</untrusted>", Trust.Untrusted), (shown.Node.Label, shown.Node.Trust));
        var neighbor = Assert.Single(hood.Nodes, n => n.Node.Key.Value == mentions);
        Assert.Equal(("<untrusted source=\"map\">Mentions in</untrusted>", Trust.Untrusted), (neighbor.Node.Label, neighbor.Node.Trust));
        Assert.Equal(("Mentions in", Trust.Untrusted), (human.Node.Label, human.Node.Trust));   // why: a human reads it clean, beside the flag
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_anchor_s_lenses_fence_its_title_for_a_session_and_report_untrusted_beside_a_clean_label_for_a_human()
    {
        await ImportMap();
        const string mentions = "record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02";
        const string fenced = "<untrusted source=\"map\">Mentions in</untrusted>";

        var session = (await _world.Engine.Lenses(mentions, read: MapRead.Current, depth: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value;
        var human = (await _world.Engine.Lenses(mentions, read: MapRead.Current, depth: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal((fenced, Trust.Untrusted), (session.Anchor.Label, session.Anchor.Trust));
        var own = Assert.Single(session.Bands.Product.Own, i => i.Node.Value == mentions);
        Assert.Equal((fenced, Trust.Untrusted), (own.Label, own.Trust));
        Assert.Equal(("Mentions in", Trust.Untrusted), (human.Anchor.Label, human.Anchor.Trust));
        var humanOwn = Assert.Single(human.Bands.Product.Own, i => i.Node.Value == mentions);
        Assert.Equal(("Mentions in", Trust.Untrusted), (humanOwn.Label, humanOwn.Trust));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_s_map_status_counts_exactly_the_nodes_it_implements_and_the_intent_linked_to_them()
    {
        await ImportMap();

        var status = (await _world.Map.MapStatus(anchor: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value;
        var denied = (await _world.Map.MapStatus(anchor: null, SessionDenying("docs/private/**"), MapWorld.None)).Value;

        // feature:in, code:src/in.ts, records a01 (quoted by the in decision), a02 and c02, the in decision, and the private-doc record and its decision (open: no deny glob here);
        // edges: implements, 2 mentions, 2 decides, 2 quotes. Not feature:out, b02, the out decision or record b01. The alias to a02 and the two decisions stay; the alias to b02 and the out decision go.
        Assert.Equal(new MapCounts(8, 7, 1, 2, 0), status.Counts);
        Assert.Equal(new MapCounts(6, 5, 1, 1, 0), denied.Counts);   // why: denying docs/private/** takes the private record, its decision and their two edges with it
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_a_visible_decision_quotes_is_shown_with_only_its_in_scope_mentions_and_is_003_when_the_quoted_doc_is_denied()
    {
        await ImportMap();
        var session = MapWorld.Session(FixtureMap.Repo);

        var quoted = (await _world.Engine.ShowNode("record:" + RecordInDecision, session, MapWorld.None)).Value;
        var human = (await _world.Engine.ShowNode("record:" + RecordInDecision, MapWorld.Human, MapWorld.None)).Value;
        var privateOpen = await _world.Engine.ShowNode("record:" + PrivateDoc, session, MapWorld.None);
        var privateDenied = await _world.Engine.ShowNode("record:" + PrivateDoc, SessionDenying("docs/private/**"), MapWorld.None);

        Assert.DoesNotContain(quoted.Out, e => e.To.Value == OutKey);
        Assert.Contains(human.Out, e => e.To.Value == OutKey);
        Assert.True(privateOpen.IsSuccess);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, privateDenied.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_alias_to_a_record_resolves_for_a_session_when_the_record_is_linked_in_scope_and_is_003_without_its_title_when_not()
    {
        await ImportMap();
        var session = MapWorld.Session(FixtureMap.Repo);

        var inScope = await _world.Engine.ShowNode(AliasIn, session, MapWorld.None);
        var outOfScope = await _world.Engine.ShowNode(AliasOut, session, MapWorld.None);

        Assert.Equal("record:linear:issue/0e5a1b00-0000-4000-8000-000000000a02", inScope.Value.Node.Key.Value);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, outOfScope.Error.Code);
        Assert.DoesNotContain("ZEBRA", outOfScope.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_or_decision_linked_only_to_out_of_scope_nodes_is_003_without_its_title()
    {
        await ImportMap();
        var session = MapWorld.Session(FixtureMap.Repo);
        var keys = new[] { OutKey, "record:linear:issue/0e5a1b00-0000-4000-8000-000000000b02", DecisionKey(RecordOutDecision, QuoteOut), "record:" + RecordOutDecision };

        foreach (var key in keys)
        {
            var shown = await _world.Engine.ShowNode(key, session, MapWorld.None);
            var lens = await _world.Engine.Lenses(key, read: MapRead.Current, depth: null, session, MapWorld.None);

            Assert.Equal((ContextErrors.UnknownAnchor.Code, ContextErrors.UnknownAnchor.Code), (shown.Error.Code, lens.Error.Code));
            Assert.DoesNotContain("ZEBRA", shown.Error.Message + lens.Error.Message, StringComparison.Ordinal);
        }

        var band = (await _world.Engine.Lenses(InKey, read: MapRead.Current, depth: null, session, MapWorld.None)).Value.Bands.Intent;
        Assert.DoesNotContain(band.Records, r => r.Label.Contains("ZEBRA", StringComparison.Ordinal));
        Assert.DoesNotContain(band.Decisions, d => d.Node.Value == DecisionKey(RecordOutDecision, QuoteOut));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_node_query_carries_the_decisions_deciding_its_nodes_fenced_and_not_the_others()
    {
        await ImportMap();

        var result = (await _world.Engine.Query(new ContextQuery(Text: null, Node: InKey), MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value;

        var decisions = result.Sections.Single(s => s.Name == "decisions").Items;
        var item = Assert.Single(decisions, i => i.Node == DecisionKey(RecordInDecision, QuoteIn));
        Assert.Equal("<untrusted source=\"map\">" + QuoteIn + "</untrusted>", item.Text);
        Assert.DoesNotContain(decisions, i => i.Node == DecisionKey(RecordOutDecision, QuoteOut));
        Assert.DoesNotContain(FakeScrubber.Secret, item.Title, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_quoting_a_denied_document_is_gone_for_a_session_too()
    {
        await ImportMap();
        var privateDecision = DecisionKey(PrivateDoc, "private quote");
        var open = (await _world.Engine.Lenses(InKey, read: MapRead.Current, depth: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value.Bands.Intent;
        var denied = (await _world.Engine.Lenses(InKey, read: MapRead.Current, depth: null, SessionDenying("docs/private/**"), MapWorld.None)).Value.Bands.Intent;

        Assert.Contains(open.Decisions, d => d.Node.Value == privateDecision);
        Assert.DoesNotContain(denied.Decisions, d => d.Node.Value == privateDecision);
        Assert.Equal(open.Counts.Decisions - 1, denied.Counts.Decisions);
    }
}
