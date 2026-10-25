using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Testing.LensPacks;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Workflow.Lens;

/// <summary>'step.review.round' on the block runtime (line spec §5.2, DTC4, LN-PK lite): queues the review pack's lenses that apply to the changed paths, records the rest as not applicable, and carries the log.</summary>
[Lane(TestLane.Unit)]
public sealed class ReviewRoundStepTests
{
    private const string Hash = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Change = """{"repo":"web","branch":"b","base":"main","head":"abc123"}""";

    private readonly FakeChangedPaths _changed = new();
    private readonly FakeLensPacks _packs = new FakeLensPacks().With(new LensPack(LensPackId.From("code-review"), Hash, LensPackKind.Review,
    [
        new PackLens(LensId.From("correctness"), "Correctness", "prompt", LensWhen.Always),
        new PackLens(LensId.From("data-integrity"), "Data", "prompt", new LensWhen([], ["**/migrations/**", "**/*.sql"])),
        new PackLens(LensId.From("security"), "Security", "prompt", LensWhen.Always),
    ]));

    private async Task<BlockStepResult> Run(string change, string rounds)
    {
        var context = LensStepContexts.Of("step.review.round", ("change", change), ("phasePacket", "{}"), ("lensRounds", rounds)) with { Repo = RepoName.From("web") };
        return await new ReviewRoundStep(_packs, _changed, new InMemoryRepoCheckouts().With("web", "/checkouts/web")).Run(context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_matching_lens_is_queued_with_the_always_lenses_in_pack_order()
    {
        _ = _changed.With("/checkouts/web", "main", "abc123", "src/app.cs", "db/migrations/001_add.sql");

        var result = await Run(Change, "[]");

        Assert.Equal("ready", result.Result!.Outcome.Value);
        Assert.Equal(["correctness", "data-integrity", "security"], LensStepContexts.Out(result, "lensQueue").AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Empty(LensStepContexts.Out(result, "lensRounds").AsArray());
        Assert.Equal(["/checkouts/web main...abc123"], _changed.Calls, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_lens_whose_paths_match_nothing_is_not_applicable_with_its_reason_never_skipped_silently()
    {
        _ = _changed.With("/checkouts/web", "main", "abc123", "src/app.cs");

        var result = await Run(Change, "[]");

        Assert.Equal(["correctness", "security"], LensStepContexts.Out(result, "lensQueue").AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
        var entry = Assert.Single(LensStepContexts.Out(result, "lensRounds").AsArray())!;
        Assert.Equal("data-integrity", entry["lens"]!.GetValue<string>());
        Assert.Equal(1, entry["round"]!.GetValue<int>());
        Assert.Equal("no changed path matches **/migrations/**, **/*.sql", entry["notApplicable"]!.GetValue<string>());
        Assert.Equal(Hash, entry["packHash"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_second_round_carries_the_log_and_numbers_its_rows_after_the_closed_round()
    {
        _ = _changed.With("/checkouts/web", "main", "abc123", "src/app.cs");
        const string round1 = """[{"lens":"correctness","round":1,"findings":[],"closes":true}]""";

        var result = await Run(Change, round1);

        var rounds = LensStepContexts.Out(result, "lensRounds").AsArray();
        Assert.Equal([1, 2], rounds.Select(static r => r!["round"]!.GetValue<int>()));
        Assert.Equal("data-integrity", rounds[1]!["lens"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_change_that_names_no_base_and_head_has_no_known_paths_so_every_lens_is_queued()
    {
        var result = await Run("{}", "[]");

        Assert.Equal(["correctness", "data-integrity", "security"], LensStepContexts.Out(result, "lensQueue").AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Empty(_changed.Calls);
    }

    [Fact]
    public async Task No_lens_applying_reports_noLens_and_writes_nothing()
    {
        var packs = new FakeLensPacks().With(new LensPack(LensPackId.From("code-review"), Hash, LensPackKind.Review, [new PackLens(LensId.From("data-integrity"), "Data", "prompt", new LensWhen([], ["**/*.sql"]))]));
        _ = _changed.With("/checkouts/web", "main", "abc123", "src/app.cs");
        var context = LensStepContexts.Of("step.review.round", ("change", Change), ("phasePacket", "{}"), ("lensRounds", "[]")) with { Repo = RepoName.From("web") };

        var result = await new ReviewRoundStep(packs, _changed, new InMemoryRepoCheckouts().With("web", "/checkouts/web")).Run(context, TestContext.Current.CancellationToken);

        Assert.Equal("noLens", result.Result!.Outcome.Value);
        Assert.Empty(result.Result.Outputs);
    }

    [Fact]
    public async Task A_diff_that_cannot_be_listed_fails_the_Step_rather_than_reviewing_nothing()
    {
        var result = await Run(Change, "[]");   // the fake knows no pair of refs

        Assert.Null(result.Result);
        Assert.Contains("can't list the changed paths", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_log_that_is_not_an_array_fails_the_Step_with_the_fix()
    {
        var result = await Run(Change, "null");

        Assert.Null(result.Result);
        Assert.Contains("seed it with []", result.Failure, StringComparison.Ordinal);
    }
}
