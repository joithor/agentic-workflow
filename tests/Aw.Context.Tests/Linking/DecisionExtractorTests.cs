using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Linking;

/// <summary>The decision-extraction protocol (spec §6.1, §7; S-5B): the model proposes, the engine keeps only what it can check.</summary>
[Trait("Lane", "unit")]
public sealed class DecisionExtractorTests
{
    private const string Text = "Decision: toggling orientation keeps the focused pane first, so the cursor never jumps. We considered swapping the pane order and rejected it. "
        + "Decision (replaces the 10-column minimum above): the minimum pane width is 20 columns.";
    private const string Quote = "toggling orientation keeps the focused pane first";
    private const string Toggle = "surface:command/toggle-orientation";
    private static readonly RecordKey _record = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
    private static readonly NodeKey[] _linked = [NodeKey.From(Toggle), NodeKey.From("feature:layout.pane-resize")];
    private static readonly DateTimeOffset _at = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static ExtractionOutcome Read(string answer) => DecisionExtractor.Read(answer, _record, Text, _linked, _at);

    [Fact]
    public void Extract_keeps_only_quotes_found_verbatim_in_the_scrubbed_text()
    {
        var outcome = Read($$"""{"decisions":[{"quote":"{{Quote}}","wording":"Toggling keeps focus first.","decides":["{{Toggle}}"]},{"quote":"the cursor jumps to the new pane","wording":"Invented.","decides":["{{Toggle}}"]}]}""");

        var kept = Assert.Single(outcome.Decisions);
        Assert.Equal(Quote, kept.Quote);
        Assert.Equal(1, outcome.Refused);
    }

    [Fact]
    public void Extract_decides_targets_limited_to_the_records_linked_nodes()
    {
        var outcome = Read($$"""{"decisions":[{"quote":"{{Quote}}","wording":"w","decides":["{{Toggle}}","feature:somewhere.else"]}]}""");

        var edge = Assert.Single(outcome.Edges);
        Assert.Equal((EdgeType.Decides, Toggle), (edge.Type, edge.To.Value));
        Assert.Equal(1, outcome.Refused);
        Assert.Empty(Read($$"""{"decisions":[{"quote":"{{Quote}}","wording":"w","decides":["feature:somewhere.else"]}]}""").Decisions);   // a decision that decides nothing the record links is dropped
    }

    [Fact]
    public void Decision_key_is_sha256_record_and_quote()
    {
        var outcome = Read($$"""{"decisions":[{"quote":"{{Quote}}","wording":"w","decides":["{{Toggle}}"]}]}""");

        var decision = Assert.Single(outcome.Decisions);
        Assert.Equal(DecisionRow.KeyFor(_record, Quote).Value, decision.ToKey().Value);
        Assert.Equal((_record.Value, _at), (decision.Record.Value, decision.At));
        Assert.Equal(decision.ToKey().Value, Assert.Single(outcome.Edges).From.Value);
    }

    [Fact]
    public void Model_text_is_untrusted_and_unconfirmed()
    {
        var outcome = Read($$"""{"decisions":[{"quote":"{{Quote}}","wording":"w","decides":["{{Toggle}}"]}]}""");

        Assert.Equal(Provenance.Model, Assert.Single(outcome.Decisions).Provenance);
        Assert.Equal(Provenance.Model, Assert.Single(outcome.Edges).Provenance);
    }

    [Fact]
    public void Replacing_decision_in_same_record_writes_supersedes()
    {
        const string newer = "the minimum pane width is 20 columns";
        var outcome = Read($$"""{"decisions":[{"quote":"{{Quote}}","wording":"a","decides":["{{Toggle}}"]},{"quote":"{{newer}}","wording":"b","decides":["feature:layout.pane-resize"],"replaces":"{{Quote}}"}]}""");

        var supersedes = Assert.Single(outcome.Edges, e => e.Type == EdgeType.Supersedes);
        Assert.Equal((DecisionRow.KeyFor(_record, newer).Value, DecisionRow.KeyFor(_record, Quote).Value), (supersedes.From.Value, supersedes.To.Value));
        Assert.Equal(0, outcome.Refused);
    }

    [Fact]
    public void A_replaces_that_names_no_decision_of_the_answer_is_refused()
    {
        var outcome = Read($$"""{"decisions":[{"quote":"{{Quote}}","wording":"a","decides":["{{Toggle}}"],"replaces":"something else entirely"}]}""");

        Assert.DoesNotContain(outcome.Edges, e => e.Type == EdgeType.Supersedes);
        _ = Assert.Single(outcome.Decisions);
        Assert.Equal(1, outcome.Refused);
    }

    [Theory]
    [InlineData("I could not find any decisions.")]
    [InlineData("""{"decisions": "none"}""")]
    [InlineData("{")]
    public void A_malformed_answer_is_counted_refused_and_keeps_nothing(string answer)
    {
        var outcome = Read(answer);

        Assert.Empty(outcome.Decisions);
        Assert.Equal(1, outcome.Refused);
    }

    [Fact]
    public void An_answer_wrapped_in_prose_is_read_and_an_empty_one_is_not_refused()
    {
        var wrapped = Read($$"""Here you go: {"decisions":[{"quote":"{{Quote}}","wording":"w","decides":["{{Toggle}}"]}]} Hope that helps.""");

        _ = Assert.Single(wrapped.Decisions);
        Assert.Equal(0, Read("""{"decisions":[]}""").Refused);
    }

    [Fact]
    public void The_prompt_names_the_record_and_its_linked_nodes_and_fences_the_text()
    {
        var prompt = DecisionExtractor.Prompt(_record, Text, _linked);

        Assert.Contains(_record.Value, prompt, StringComparison.Ordinal);
        Assert.Contains(Toggle, prompt, StringComparison.Ordinal);
        Assert.Contains(Quote, prompt, StringComparison.Ordinal);
        Assert.Contains("untrusted", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
