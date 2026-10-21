using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.LensLoops;

namespace Aw.Sindri.Tests.Workflow.Lens;

/// <summary>
/// 'step.review.log' on the block runtime (line spec §7.4, DTC4): appends the lens report; 'next' while a lens is queued; at the round's end clean, findings or behaviourWrong
/// with the running fact:review. A finding that carries a typed 'escalate' (ambiguousFix or productQuestion) makes the round's end 'needsOwner'; nothing else reaches a human.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ReviewLogStepTests
{
    private const string Step = "step.review.log";

    private static async Task<BlockStepResult> Run(string report, string rounds, string queue) =>
        await new ReviewLogStep().Run(LensStepContexts.Of(Step, ("lensReport", report), ("lensRounds", rounds), ("lensQueue", queue)), TestContext.Current.CancellationToken);

    private static string Finding(string severity, string extra = "") => $$"""{"lens":"correctness","findings":[{"severity":"{{severity}}","text":"t"{{extra}}}]}""";

    [Fact]
    public async Task A_queued_lens_left_reports_next_with_the_lens_taken_off_the_queue()
    {
        var result = await Run("""{"lens":"correctness","findings":[]}""", "[]", """["correctness","security"]""");

        Assert.Equal("next", result.Result!.Outcome.Value);
        Assert.Equal(["lensQueue", "lensRounds"], LensStepContexts.Keys(result));
        Assert.Equal(["security"], LensStepContexts.Out(result, "lensQueue").AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_round_with_only_suggestions_and_nits_ends_clean_with_the_running_review()
    {
        var result = await Run("""{"lens":"correctness","findings":[{"severity":"suggestion","text":"s"},{"severity":"nit","text":"n"}]}""", "[]", """["correctness"]""");

        Assert.Equal("clean", result.Result!.Outcome.Value);
        Assert.Equal(["lensRounds", "review"], LensStepContexts.Keys(result));
        var review = LensStepContexts.Out(result, "review");
        Assert.Equal("clean", review["verdict"]!.GetValue<string>());
        Assert.Equal(1, review["round"]!.GetValue<int>());
        Assert.Equal((0, 0, 1, 1), (review["blocking"]!.GetValue<int>(), review["issue"]!.GetValue<int>(), review["suggestions"]!.GetValue<int>(), review["nits"]!.GetValue<int>()));
    }

    [Fact]
    public async Task A_blocking_or_issue_finding_ends_the_round_with_findings_that_name_their_lens()
    {
        var result = await Run(Finding("issue"), "[]", """["correctness"]""");

        Assert.Equal("findings", result.Result!.Outcome.Value);
        var review = LensStepContexts.Out(result, "review");
        Assert.Equal("findings", review["verdict"]!.GetValue<string>());
        Assert.Equal(1, review["issue"]!.GetValue<int>());
        Assert.Equal("correctness", Assert.Single(review["findings"]!.AsArray())!["lens"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_blocking_finding_whose_behaviour_is_wrong_ends_the_round_behaviourWrong()
    {
        var result = await Run(Finding("blocking", ",\"behaviour\":\"wrong\""), "[]", """["correctness"]""");

        Assert.Equal("behaviourWrong", result.Result!.Outcome.Value);
        Assert.Equal("wrong", LensStepContexts.Out(result, "review")["behaviour"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_round_end_counts_the_findings_of_every_lens_of_the_round_and_numbers_the_next_round()
    {
        const string earlier = """[{"round":1,"lens":"security","findings":[{"severity":"blocking","text":"b"}],"closes":true},{"round":2,"lens":"security","findings":[{"severity":"nit","text":"n"}]}]""";

        var result = await Run("""{"lens":"correctness","findings":[{"severity":"issue","text":"i"}]}""", earlier, """["correctness"]""");

        var review = LensStepContexts.Out(result, "review");
        Assert.Equal(2, review["round"]!.GetValue<int>());
        Assert.Equal((0, 1, 1), (review["blocking"]!.GetValue<int>(), review["issue"]!.GetValue<int>(), review["nits"]!.GetValue<int>()));
        Assert.Equal(3, LensStepContexts.Out(result, "lensRounds").AsArray().Count);
    }

    [Fact]
    public async Task A_typed_ambiguousFix_on_a_blocking_finding_ends_the_round_needsOwner_with_the_question()
    {
        var result = await Run(Finding("blocking", ",\"id\":\"f1\",\"escalate\":{\"kind\":\"ambiguousFix\",\"question\":\"Cache or recompute?\",\"options\":[\"cache\",\"recompute\"]}"), "[]", """["correctness"]""");

        Assert.Equal("needsOwner", result.Result!.Outcome.Value);
        Assert.Equal(["escalation", "lensRounds", "review"], LensStepContexts.Keys(result));
        var escalation = LensStepContexts.Out(result, "escalation");
        Assert.Equal("ambiguousFix", escalation["kind"]!.GetValue<string>());
        Assert.Equal("Cache or recompute?", escalation["question"]!.GetValue<string>());
        Assert.Equal(["cache", "recompute"], escalation["options"]!.AsArray().Select(static o => o!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal("correctness", escalation["lens"]!.GetValue<string>());
        Assert.Equal("findings", LensStepContexts.Out(result, "review")["verdict"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_typed_productQuestion_on_an_issue_finding_ends_the_round_needsOwner_without_options()
    {
        var result = await Run(Finding("issue", ",\"escalate\":{\"kind\":\"productQuestion\",\"question\":\"Should expired items be hidden?\"}"), "[]", """["correctness"]""");

        Assert.Equal("needsOwner", result.Result!.Outcome.Value);
        var escalation = LensStepContexts.Out(result, "escalation");
        Assert.Equal("productQuestion", escalation["kind"]!.GetValue<string>());
        Assert.Null(escalation["options"]);
    }

    [Theory]
    [InlineData(",\"text2\":\"please decide whether to cache\"")]
    [InlineData(",\"escalate\":\"please decide\"")]
    [InlineData(",\"escalate\":{\"kind\":\"pleaseDecide\",\"question\":\"q\"}")]
    [InlineData(",\"escalate\":{\"kind\":\"ambiguousFix\"}")]
    [InlineData(",\"escalate\":{\"kind\":\"ambiguousFix\",\"question\":\"\"}")]
    [InlineData(",\"escalate\":{\"kind\":\"ambiguousFix\",\"question\":\"q\",\"options\":[1]}")]
    [InlineData(",\"escalate\":{\"question\":\"q\"}")]
    public async Task A_finding_without_a_typed_escalation_stays_in_the_loop(string extra)
    {
        var result = await Run(Finding("blocking", extra), "[]", """["correctness"]""");

        Assert.Equal("findings", result.Result!.Outcome.Value);
        Assert.DoesNotContain("escalation", LensStepContexts.Keys(result), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_escalation_on_a_suggestion_never_reaches_a_human()
    {
        var result = await Run(Finding("suggestion", ",\"escalate\":{\"kind\":\"productQuestion\",\"question\":\"q\"}"), "[]", """["correctness"]""");

        Assert.Equal("clean", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_wrong_behaviour_wins_over_an_escalation()
    {
        const string both = """{"lens":"correctness","findings":[{"severity":"blocking","behaviour":"wrong","text":"w"},{"severity":"issue","text":"i","escalate":{"kind":"ambiguousFix","question":"q"}}]}""";

        var result = await Run(both, "[]", """["correctness"]""");

        Assert.Equal("behaviourWrong", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task An_escalation_waits_for_the_round_end_and_stays_in_the_log()
    {
        var mid = await Run(Finding("blocking", ",\"escalate\":{\"kind\":\"ambiguousFix\",\"question\":\"q\"}"), "[]", """["correctness","security"]""");

        Assert.Equal("next", mid.Result!.Outcome.Value);
        var end = await Run("""{"lens":"security","findings":[]}""", LensStepContexts.Out(mid, "lensRounds").ToJsonString(), """["security"]""");
        Assert.Equal("needsOwner", end.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_severity_off_the_review_scale_fails_the_Step()
    {
        var result = await Run("""{"lens":"correctness","findings":[{"severity":"HIGH","text":"x"}]}""", "[]", """["correctness"]""");

        Assert.Null(result.Result);
    }
}
