using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Linking;

/// <summary>Intent-vs-intent conflicts (spec §8.9, A-F13): which pairs are asked about, and how the verdict is read.</summary>
[Trait("Lane", "unit")]
public sealed class ConflictCheckerTests
{
    private const string Node = "surface:command/toggle-orientation";
    private static readonly DateTimeOffset _at = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly RecordKey _r103 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
    private static readonly RecordKey _r104 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000104");

    private static DecisionRow Decision(RecordKey record, string quote) => new(record, quote, "wording of " + quote, Provenance.Model, _at);

    private static GraphEdge Decides(DecisionRow d, string node = Node) => new(d.ToKey(), NodeKey.From(node), EdgeType.Decides, Provenance.Model, 0.8, At: _at);

    private static IntentLayer Layer(DecisionRow[] decisions, GraphEdge[] edges, DecisionConflict[]? checkedPairs = null) =>
        new([], [], decisions, edges) { Conflicts = checkedPairs ?? [] };

    [Fact]
    public void Two_current_decisions_on_one_node_are_a_pair_to_ask_about()
    {
        var (a, b) = (Decision(_r103, "keeps focus first"), Decision(_r104, "resets focus to the first pane"));

        var pairs = ConflictChecker.Pairs(Layer([a, b], [Decides(a), Decides(b)]), [b]);

        var pair = Assert.Single(pairs);
        Assert.Equal(Node, pair.Node.Value);
        Assert.Equal(new[] { a.ToKey().Value, b.ToKey().Value }.Order(StringComparer.Ordinal), new[] { pair.First.ToKey().Value, pair.Second.ToKey().Value }.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Conflict_check_is_asked_once_per_pair_and_version()
    {
        var (a, b) = (Decision(_r103, "keeps focus first"), Decision(_r104, "resets focus"));
        var done = DecisionConflict.Of(a.ToKey(), b.ToKey(), ConflictVerdict.Agrees, _at);

        Assert.Empty(ConflictChecker.Pairs(Layer([a, b], [Decides(a), Decides(b)], [done]), [b]));
        var changed = Decision(_r104, "now says something else");   // a new version of the decision is a new key, so a new pair
        _ = Assert.Single(ConflictChecker.Pairs(Layer([a, changed], [Decides(a), Decides(changed)], [done]), [changed]));
    }

    [Fact]
    public void A_superseded_decision_is_not_current_and_decisions_on_other_nodes_are_no_pair()
    {
        var (old, newer, elsewhere) = (Decision(_r103, "ten columns"), Decision(_r103, "twenty columns"), Decision(_r104, "unrelated"));
        var supersedes = new GraphEdge(newer.ToKey(), old.ToKey(), EdgeType.Supersedes, Provenance.Model, 0.8, At: _at);

        var pairs = ConflictChecker.Pairs(Layer([old, newer, elsewhere], [Decides(old), Decides(newer), Decides(elsewhere, "feature:other"), supersedes]), [newer, elsewhere]);

        Assert.Empty(pairs);
    }

    [Theory]
    [InlineData("""{"verdict":"contradicts"}""", ConflictVerdict.Contradicts)]
    [InlineData("""Sure. {"verdict":"agrees"}""", ConflictVerdict.Agrees)]
    [InlineData("""{"verdict":"supersedes"}""", ConflictVerdict.Supersedes)]
    public void The_verdict_is_read_from_the_answer(string answer, ConflictVerdict expected) => Assert.Equal(expected, ConflictChecker.Read(answer));

    [Theory]
    [InlineData("they conflict")]
    [InlineData("""{"verdict":"maybe"}""")]
    [InlineData("{")]
    public void An_answer_that_is_not_a_verdict_is_null(string answer) => Assert.Null(ConflictChecker.Read(answer));

    [Fact]
    public void The_prompt_holds_both_quotes()
    {
        var (a, b) = (Decision(_r103, "keeps focus first"), Decision(_r104, "resets focus"));

        var prompt = ConflictChecker.Prompt(a, b);

        Assert.Contains("keeps focus first", prompt, StringComparison.Ordinal);
        Assert.Contains("resets focus", prompt, StringComparison.Ordinal);
    }
}
