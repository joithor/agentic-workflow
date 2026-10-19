using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features;

namespace Aw.Sindri.Tests.Workflow.Lens;

/// <summary>'step.review.log' on the block runtime (line spec DTC4): next while the queue is not empty; at the round's end clean (0 blocking, 0 issue), findings or behaviourWrong, with the running 'fact:review'.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewLogStepTests
{
    private static async Task<BlockStepResult> Run(string report, string rounds, string queue) =>
        await new ReviewLogStep().Run(LensStepContexts.Of("step.review.log", ("lensReport", report), ("lensRounds", rounds), ("lensQueue", queue)), TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_queued_lens_left_reports_next_and_writes_no_review()
    {
        var result = await Run("""{"lens":"correctness","findings":[{"severity":"blocking","text":"b"}]}""", "[]", """["correctness","security"]""");

        Assert.Equal("next", result.Result!.Outcome.Value);
        Assert.Equal(["lensQueue", "lensRounds"], LensStepContexts.Keys(result));
    }

    [Fact]
    public async Task A_round_with_no_blocking_and_no_issue_is_clean_and_counts_every_class()
    {
        var result = await Run("""{"lens":"security","findings":[{"severity":"suggestion","text":"s"},{"severity":"nit","text":"n"}]}""", "[]", """["security"]""");

        Assert.Equal("clean", result.Result!.Outcome.Value);
        var review = LensStepContexts.Out(result, "review");
        Assert.Equal(0, review["blocking"]!.GetValue<int>());
        Assert.Equal(0, review["issue"]!.GetValue<int>());
        Assert.Equal(1, review["suggestions"]!.GetValue<int>());
        Assert.Equal(1, review["nits"]!.GetValue<int>());
        Assert.Equal("clean", review["verdict"]!.GetValue<string>());
        Assert.Equal(1, review["round"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("blocking")]
    [InlineData("issue")]
    public async Task A_blocking_or_issue_finding_makes_the_round_findings(string severity)
    {
        var result = await Run($$"""{"lens":"security","findings":[{"severity":"{{severity}}","text":"x"}]}""", "[]", """["security"]""");

        Assert.Equal("findings", result.Result!.Outcome.Value);
        Assert.Equal("findings", LensStepContexts.Out(result, "review")["verdict"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_blocking_finding_that_shows_the_behaviour_wrong_is_behaviourWrong()
    {
        var result = await Run("""{"lens":"correctness","findings":[{"severity":"blocking","text":"x","behaviour":"wrong"}]}""", "[]", """["correctness"]""");

        Assert.Equal("behaviourWrong", result.Result!.Outcome.Value);
        Assert.Equal(1, LensStepContexts.Out(result, "review")["blocking"]!.GetValue<int>());
    }

    [Fact]
    public async Task The_review_keeps_every_finding_of_the_round_with_its_lens()
    {
        const string earlier = """[{"round":1,"lens":"correctness","findings":[{"severity":"issue","text":"i"}]}]""";

        var result = await Run("""{"lens":"security","findings":[{"severity":"nit","text":"n"}]}""", earlier, """["security"]""");

        var findings = LensStepContexts.Out(result, "review")["findings"]!.AsArray();
        Assert.Equal(["correctness", "security"], findings.Select(static f => f!["lens"]!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Another_loop_scale_is_refused()
    {
        var result = await Run("""{"lens":"security","findings":[{"severity":"CRITICAL","text":"x"}]}""", "[]", """["security"]""");

        Assert.Null(result.Result);
    }
}
