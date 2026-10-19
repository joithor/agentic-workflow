using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features;

namespace Aw.Sindri.Tests.Workflow.Lens;

/// <summary>'step.lens.log' on the block runtime (line spec §4.1): appends the report, then next while a lens is queued, and at the round's end clean (0 CRITICAL, 0 HIGH) or findings.</summary>
[Lane(TestLane.Unit)]
public sealed class LensLogStepTests
{
    private const string Step = "step.lens.log";

    private static async Task<BlockStepResult> Run(string report, string rounds, string queue) =>
        await new LensLogStep().Run(LensStepContexts.Of(Step, ("lensReport", report), ("lensRounds", rounds), ("lensQueue", queue)), TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_queued_lens_left_reports_next_with_the_lens_taken_off_the_queue()
    {
        var result = await Run("""{"lens":"product","findings":[]}""", "[]", """["product","arch","security"]""");

        Assert.Equal("next", result.Result!.Outcome.Value);
        Assert.Equal(["lensQueue", "lensRounds"], LensStepContexts.Keys(result));
        Assert.Equal(["arch", "security"], LensStepContexts.Out(result, "lensQueue").AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
        var entry = Assert.Single(LensStepContexts.Out(result, "lensRounds").AsArray())!;
        Assert.Equal("product", entry["lens"]!.GetValue<string>());
        Assert.Equal(1, entry["round"]!.GetValue<int>());
        Assert.Null(entry["closes"]);
    }

    [Fact]
    public async Task The_last_lens_with_nothing_blocking_reports_clean_and_closes_the_round()
    {
        var result = await Run("""{"lens":"security","findings":[{"severity":"MEDIUM","text":"a"},{"severity":"LOW","text":"b"}]}""", "[]", """["security"]""");

        Assert.Equal("clean", result.Result!.Outcome.Value);
        Assert.Equal(["lensRounds"], LensStepContexts.Keys(result));
        Assert.True(LensStepContexts.Out(result, "lensRounds").AsArray()[0]!["closes"]!.GetValue<bool>());
    }

    [Fact]
    public async Task The_last_lens_with_a_critical_or_high_finding_reports_findings_with_only_those()
    {
        var result = await Run("""{"lens":"security","findings":[{"severity":"HIGH","text":"h"},{"severity":"MEDIUM","text":"m"}]}""", "[]", """["security"]""");

        Assert.Equal("findings", result.Result!.Outcome.Value);
        var findings = LensStepContexts.Out(result, "findings").AsArray();
        var only = Assert.Single(findings)!;
        Assert.Equal("h", only["text"]!.GetValue<string>());
        Assert.Equal("security", only["lens"]!.GetValue<string>());
    }

    [Fact]
    public async Task The_round_end_counts_the_findings_of_every_lens_of_the_round()
    {
        const string earlier = """[{"round":1,"lens":"product","findings":[{"severity":"CRITICAL","text":"c"}]}]""";

        var result = await Run("""{"lens":"arch","findings":[]}""", earlier, """["arch"]""");

        Assert.Equal("findings", result.Result!.Outcome.Value);
        Assert.Equal(2, LensStepContexts.Out(result, "lensRounds").AsArray().Count);
        Assert.Equal("c", Assert.Single(LensStepContexts.Out(result, "findings").AsArray())!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_new_round_starts_after_a_closed_one_and_ignores_its_findings()
    {
        const string closed = """[{"round":1,"lens":"product","findings":[{"severity":"HIGH","text":"old"}],"closes":true}]""";

        var result = await Run("""{"lens":"product","findings":[]}""", closed, """["product"]""");

        Assert.Equal("clean", result.Result!.Outcome.Value);
        var entries = LensStepContexts.Out(result, "lensRounds").AsArray();
        Assert.Equal(2, entries.Count);
        Assert.Equal(2, entries[1]!["round"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_lens_that_is_not_applicable_needs_no_findings()
    {
        var result = await Run("""{"lens":"design","notApplicable":"no surface"}""", "[]", """["design"]""");

        Assert.Equal("clean", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_report_for_a_lens_that_is_not_queued_fails_the_Step()
    {
        var result = await Run("""{"lens":"ghost","findings":[]}""", "[]", """["product"]""");

        Assert.Null(result.Result);
        Assert.Contains("ghost", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_severity_off_the_scale_fails_the_Step()
    {
        var result = await Run("""{"lens":"product","findings":[{"severity":"blocking","text":"x"}]}""", "[]", """["product"]""");

        Assert.Null(result.Result);
        Assert.Contains("blocking", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_report_with_neither_findings_nor_a_reason_fails_the_Step()
    {
        var result = await Run("""{"lens":"product"}""", "[]", """["product"]""");

        Assert.Null(result.Result);
    }

    [Fact]
    public async Task A_missing_fact_fails_the_Step_naming_it()
    {
        var result = await new LensLogStep().Run(LensStepContexts.Of(Step, ("lensRounds", "[]"), ("lensQueue", """["a"]""")), TestContext.Current.CancellationToken);

        Assert.Null(result.Result);
        Assert.Contains("fact:lensReport", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Facts_that_arrive_as_values_not_text_are_read_too()
    {
        var facts = new Dictionary<FactKey, JsonElement>
        {
            [FactKey.From("fact:lensReport")] = JsonDocument.Parse("""{"lens":"a","findings":[]}""").RootElement.Clone(),
            [FactKey.From("fact:lensRounds")] = JsonDocument.Parse("[]").RootElement.Clone(),
            [FactKey.From("fact:lensQueue")] = JsonDocument.Parse("""["a"]""").RootElement.Clone(),
        };

        var result = await new LensLogStep().Run(new StepContext(BlockId.From(Step), new Dictionary<string, JsonElement>(StringComparer.Ordinal), facts, InjectionSections.Empty), TestContext.Current.CancellationToken);

        Assert.Equal("clean", result.Result!.Outcome.Value);
    }
}
