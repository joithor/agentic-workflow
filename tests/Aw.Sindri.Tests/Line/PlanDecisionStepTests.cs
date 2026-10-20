using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Line;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Scoping.Steps;

namespace Aw.Sindri.Tests.Line;

/// <summary>The plan station's two pure Steps on the block runtime (line spec §6.1): questions turns the design's open decisions into the stop's form, settle compares the answers with the assumptions.</summary>
[Lane(TestLane.Unit)]
public sealed class PlanDecisionStepTests
{
    private static string Decision(int n, bool recommendA = true, bool recommendB = false)
    {
        var id = "D" + n.ToString(CultureInfo.InvariantCulture);
        return """{"id":"ID","question":"Where does link expiry run?","affects":["sec-4"],"cites":["S2","AC-4"],"status":"open","options":[{"id":"ID.a","answer":"On read.","tradeoffs":"Exact.","recommended":RA},{"id":"ID.b","answer":"Nightly.","tradeoffs":"Cheap.","recommended":RB}]}"""
            .Replace("ID", id, StringComparison.Ordinal)
            .Replace("RA", recommendA ? "true" : "false", StringComparison.Ordinal)
            .Replace("RB", recommendB ? "true" : "false", StringComparison.Ordinal);
    }

    private static string Design(params string[] decisions) => """{"sections":[],"decisions":[DECISIONS]}""".Replace("DECISIONS", string.Join(',', decisions), StringComparison.Ordinal);

    private static string[] Ids(BlockStepResult result) => [.. Out(result, "questions").AsArray().Select(static q => (string)q!["id"]!)];

    private static async Task<BlockStepResult> Questions(string design, string answerLog = "[]") =>
        await new PlanQuestionsStep().Run(ScopingStepContexts.Of("step.plan.questions", new ScopingFacts { ["design"] = design, ["answers"] = "[]", ["answerLog"] = answerLog }), TestContext.Current.CancellationToken);

    private static async Task<BlockStepResult> Settle(string design, string answers, string answerLog = "[]") =>
        await new PlanSettleStep().Run(ScopingStepContexts.Of("step.plan.settle", new ScopingFacts { ["design"] = design, ["answers"] = answers, ["answerLog"] = answerLog }), TestContext.Current.CancellationToken);

    private static JsonNode Out(BlockStepResult result, string key) => JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:" + key)].GetRawText())!;

    [Fact]
    public async Task Questions_asks_each_open_decision_with_its_recommendation_marked()
    {
        var result = await Questions(Design(Decision(1), Decision(2, recommendA: false, recommendB: true)));

        Assert.Equal("asked", result.Result!.Outcome.Value);
        var asked = Out(result, "questions").AsArray();
        Assert.Equal(["Q1", "Q2"], Ids(result), StringComparer.Ordinal);
        Assert.Equal(["Q1.a", "Q1.b"], asked[0]!["options"]!.AsArray().Select(static o => (string)o!["id"]!), StringComparer.Ordinal);
        Assert.True((bool)asked[0]!["options"]![0]!["recommended"]!);
        Assert.True((bool)asked[1]!["options"]![1]!["recommended"]!);
        Assert.True((bool)asked[0]!["other"]!);
        Assert.True((bool)asked[0]!["skip"]!);
    }

    [Fact]
    public async Task Questions_does_not_ask_a_decision_the_log_already_answers_in_either_batch_shape()
    {
        var flat = await Questions(Design(Decision(1), Decision(2)), """[{"id":"A1","question":"Q1","pick":"a"}]""");
        var batch = await Questions(Design(Decision(1), Decision(2)), """[{"id":"A1","answers":[{"question":"Q2","option":"Q2.b"}]}]""");

        Assert.Equal(["Q2"], Ids(flat), StringComparer.Ordinal);
        Assert.Equal(["Q1"], Ids(batch), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Questions_reports_none_and_writes_nothing_when_no_decision_is_open()
    {
        var empty = await Questions(Design());
        var answered = await Questions(Design(Decision(1)), """[{"id":"A1","question":"Q1","pick":"a"}]""");

        Assert.Equal("none", empty.Result!.Outcome.Value);
        Assert.Empty(empty.Result.Outputs);
        Assert.Equal("none", answered.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Questions_asks_every_open_decision_in_decision_order_however_many_there_are()
    {
        var result = await Questions(Design(Decision(1), Decision(2), Decision(3), Decision(4), Decision(5), Decision(6), Decision(7)));

        Assert.Equal(["Q1", "Q2", "Q3", "Q4", "Q5", "Q6", "Q7"], Ids(result), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Questions_fails_a_decision_without_exactly_one_recommended_option()
    {
        var result = await Questions(Design(Decision(3, recommendA: false)));

        Assert.Null(result.Result);
        Assert.Contains("D3", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Settle_with_every_pick_on_the_recommendation_is_assumed_and_writes_nothing()
    {
        var result = await Settle(Design(Decision(1), Decision(2)), """[{"question":"Q1","option":"Q1.a"},{"question":"Q2","pick":"a"}]""");

        Assert.Equal("assumed", result.Result!.Outcome.Value);
        Assert.Empty(result.Result.Outputs);
    }

    [Fact]
    public async Task Settle_with_a_pick_off_the_recommendation_is_changed_and_names_the_decision()
    {
        var result = await Settle(Design(Decision(1), Decision(2)), """[{"question":"Q1","pick":"a"},{"question":"Q2","pick":"b"}]""");

        Assert.Equal("changed", result.Result!.Outcome.Value);
        var revision = Out(result, "designRevision");
        Assert.Equal("D2", (string)revision["decisions"]![0]!["decision"]!);
        Assert.Equal("D2.b", (string)revision["decisions"]![0]!["option"]!);
        _ = Assert.Single(revision["decisions"]!.AsArray());
    }

    [Fact]
    public async Task Settle_reads_only_the_latest_batch_when_the_answers_are_batches()
    {
        const string answers = """[{"id":"x","answers":[{"question":"Q1","option":"Q1.b"}]},{"id":"y","answers":[{"question":"Q1","option":"Q1.a"}]}]""";

        var result = await Settle(Design(Decision(1)), answers);

        Assert.Equal("assumed", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Settle_with_other_text_on_a_decision_is_changed_and_a_skip_is_not()
    {
        var other = await Settle(Design(Decision(1)), """[{"question":"Q1","other":"Neither: run it on write."}]""");
        var skipped = await Settle(Design(Decision(1)), """[{"question":"Q1","skip":true}]""");

        Assert.Equal("changed", other.Result!.Outcome.Value);
        Assert.Equal("Neither: run it on write.", (string)Out(other, "designRevision")["decisions"]![0]!["text"]!);
        Assert.Equal("assumed", skipped.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Settle_with_a_design_note_is_changed_and_with_a_plan_note_is_replan_and_design_wins()
    {
        var design = await Settle(Design(Decision(1)), """[{"question":"Q-revise-design","other":"Cover the reminder."}]""");
        var plan = await Settle(Design(Decision(1)), """[{"question":"Q-revise-plan","other":"Split P1."}]""");
        var both = await Settle(Design(Decision(1)), """[{"question":"Q-revise-plan","other":"Split P1."},{"question":"Q-revise-design","other":"Cover it."}]""");

        Assert.Equal("changed", design.Result!.Outcome.Value);
        Assert.Equal("Cover the reminder.", (string)Out(design, "designRevision")["notes"]![0]!);
        Assert.Equal("replan", plan.Result!.Outcome.Value);
        Assert.Empty(plan.Result.Outputs);
        Assert.Equal("changed", both.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Settle_with_a_design_revision_and_no_text_still_carries_a_note_so_it_is_a_revision()
    {
        var result = await Settle(Design(Decision(1)), """[{"question":"Q-revise-design"}]""");

        Assert.Equal("changed", result.Result!.Outcome.Value);
        Assert.Equal(["owner asked for a revision, no text"], Out(result, "designRevision")["notes"]!.AsArray().Select(static n => (string)n!), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Q-planX")]
    [InlineData("Q-design0")]
    [InlineData("Q-revise-planning")]
    public async Task Settle_matches_the_two_revise_ids_exactly_so_any_other_id_is_a_decision_answer_and_fails(string id)
    {
        var result = await Settle(Design(Decision(1)), $$"""[{"question":"{{id}}","other":"x"}]""");

        Assert.Null(result.Result);
        Assert.Contains("matches no decision", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Settle_with_no_answers_is_assumed()
    {
        var result = await Settle(Design(Decision(1)), "[]");

        Assert.Equal("assumed", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task Settle_fails_an_answer_that_matches_no_decision_or_option()
    {
        var question = await Settle(Design(Decision(1)), """[{"question":"Q9","pick":"a"}]""");
        var option = await Settle(Design(Decision(1)), """[{"question":"Q1","pick":"z"}]""");

        Assert.Contains("Q9", question.Failure, StringComparison.Ordinal);
        Assert.Contains("z", option.Failure, StringComparison.Ordinal);
    }
}
