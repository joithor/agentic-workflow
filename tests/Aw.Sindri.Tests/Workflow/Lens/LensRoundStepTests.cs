using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.LensLoops;
using Aw.Sindri.Testing.LensPacks;

namespace Aw.Sindri.Tests.Workflow.Lens;

/// <summary>'step.lens.round' on the block runtime (line spec §5.2, LN-PK lite): queues the design pack's lenses that apply to the scope, records the rest as not applicable, and carries the log.</summary>
[Lane(TestLane.Unit)]
public sealed class LensRoundStepTests
{
    private const string Hash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static LensPack Design(params PackLens[] lenses) => new(LensPackId.From("design-five"), Hash, LensPackKind.Design, lenses);

    private static PackLens Always(string id) => new(LensId.From(id), id, "prompt", LensWhen.Always);

    private static async Task<BlockStepResult> Run(FakeLensPacks packs, string rounds, string? scope = null)
    {
        var facts = new List<(string, string)> { ("packs", "{}"), ("lensRounds", rounds), ("changedSections", "[]") };
        if (scope is not null)
        {
            facts.Add(("scope", scope));
        }

        var context = LensStepContexts.Of("step.lens.round", [.. facts]) with { Repo = RepoName.From("web") };
        return await new LensRoundStep(packs).Run(context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Round_one_queues_every_always_lens_in_pack_order_on_an_empty_log()
    {
        var packs = new FakeLensPacks().With(Design(Always("product"), Always("architecture"), Always("security")));

        var result = await Run(packs, "[]");

        Assert.Equal("ready", result.Result!.Outcome.Value);
        Assert.Equal(["lensQueue", "lensRounds"], LensStepContexts.Keys(result));
        Assert.Equal(["product", "architecture", "security"], LensStepContexts.Out(result, "lensQueue").AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Empty(LensStepContexts.Out(result, "lensRounds").AsArray());
    }

    [Fact]
    public async Task The_pack_is_asked_for_by_the_instances_repo()
    {
        var packs = new FakeLensPacks().With(Design(Always("product")));

        _ = await Run(packs, "[]");

        var (kind, repo) = Assert.Single(packs.Asked);
        Assert.Equal(LensPackKind.Design, kind);
        Assert.Equal("web", repo?.Value);
    }

    [Fact]
    public async Task A_lens_whose_surface_kind_is_not_in_the_scope_is_recorded_not_applicable_with_its_reason_and_the_pack_hash()
    {
        var packs = new FakeLensPacks().With(Design(Always("product"), new PackLens(LensId.From("design"), "Design", "prompt", new LensWhen(["screen"], []))));

        var result = await Run(packs, "[]", """{"surfaces":[{"id":"S1","kind":"api"}]}""");

        Assert.Equal(["product"], LensStepContexts.Out(result, "lensQueue").AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
        var entry = Assert.Single(LensStepContexts.Out(result, "lensRounds").AsArray())!;
        Assert.Equal("design", entry["lens"]!.GetValue<string>());
        Assert.Equal(1, entry["round"]!.GetValue<int>());
        Assert.Equal("no surface of kind screen in the scope", entry["notApplicable"]!.GetValue<string>());
        Assert.Equal(Hash, entry["packHash"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_lens_whose_surface_kind_is_in_the_scope_map_is_queued()
    {
        var packs = new FakeLensPacks().With(Design(new PackLens(LensId.From("design"), "Design", "prompt", new LensWhen(["screen"], []))));

        var result = await Run(packs, "[]", """{"scopeMap":{"surfaces":[{"id":"S1","kind":"Screen"}]}}""");

        Assert.Equal(["design"], LensStepContexts.Out(result, "lensQueue").AsArray().Select(static n => n!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_later_round_carries_the_log_and_numbers_its_not_applicable_rows_after_the_closed_round()
    {
        var packs = new FakeLensPacks().With(Design(Always("product"), new PackLens(LensId.From("design"), "Design", "prompt", new LensWhen(["screen"], []))));
        const string closed = """[{"lens":"product","round":1,"findings":[],"closes":true}]""";

        var result = await Run(packs, closed, """{"surfaces":[]}""");

        var rounds = LensStepContexts.Out(result, "lensRounds").AsArray();
        Assert.Equal(2, rounds.Count);
        Assert.Equal("product", rounds[0]!["lens"]!.GetValue<string>());
        Assert.Equal(2, rounds[1]!["round"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_log_that_is_not_an_array_fails_the_Step_with_the_fix()
    {
        var result = await Run(new FakeLensPacks().With(Design(Always("product"))), "{}");

        Assert.Null(result.Result);
        Assert.Contains("seed it with []", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pack_that_cannot_be_resolved_fails_the_Step_with_the_reason()
    {
        var result = await Run(new FakeLensPacks().Choosing(LensPackKind.Design, "nope"), "[]");

        Assert.Null(result.Result);
        Assert.Contains("nope", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pack_none_of_whose_lenses_apply_fails_the_Step()
    {
        var packs = new FakeLensPacks().With(Design(new PackLens(LensId.From("design"), "Design", "prompt", new LensWhen(["screen"], []))));

        var result = await Run(packs, "[]", """{"surfaces":[]}""");

        Assert.Null(result.Result);
        Assert.Contains("no lens of the pack 'design-five' applies", result.Failure, StringComparison.Ordinal);
    }
}
