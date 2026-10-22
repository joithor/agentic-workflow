using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>The packs the toolkit ships in config/packs/lenses, read through the embedded source: what the loops run with when a profile names nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class BuiltInLensPacksTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly ProfileLensPacks _packs = new(new NoProfile(), new EmbeddedLensPackSource());

    [Fact]
    public async Task Design_resolves_to_design_five_with_the_five_autoplan_lenses()
    {
        var pack = await _packs.Resolve(LensPackKind.Design, repo: null, Token);

        Assert.Equal("design-five", pack.Value.Id.Value);
        Assert.Equal(LensPackKind.Design, pack.Value.Applies);
        Assert.Equal(["product", "architecture", "design", "devex", "security"], pack.Value.Lenses.Select(static l => l.Id.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Review_resolves_to_code_review_with_its_domains_and_path_conditions()
    {
        var pack = await _packs.Resolve(LensPackKind.Review, repo: null, Token);

        Assert.Equal("code-review", pack.Value.Id.Value);
        Assert.Equal(LensPackKind.Review, pack.Value.Applies);
        Assert.Equal(["correctness", "security", "performance", "architecture", "tests", "data-integrity", "accessibility", "ui-races"], pack.Value.Lenses.Select(static l => l.Id.Value), StringComparer.Ordinal);
        Assert.True(pack.Value.Lenses.Single(static l => l.Id.Value == "data-integrity").When.Paths.Count > 0);
        Assert.True(pack.Value.Lenses.Single(static l => l.Id.Value == "correctness").When.IsAlways);
    }

    [Fact]
    public async Task Every_built_in_pack_holds_at_most_ten_lenses_and_every_lens_a_prompt()
    {
        var source = new EmbeddedLensPackSource();

        Assert.Equal(["code-review", "design-five"], source.Ids(), StringComparer.Ordinal);
        foreach (var id in source.Ids())
        {
            var kind = id == "code-review" ? LensPackKind.Review : LensPackKind.Design;
            var pack = await new ProfileLensPacks(new NoProfile(id), source).Resolve(kind, repo: null, Token);

            Assert.True(pack.Value.Lenses.Count is > 0 and <= LensPack.MaxLenses, id);
            Assert.All(pack.Value.Lenses, l => Assert.False(string.IsNullOrWhiteSpace(l.Prompt), $"{id}/{l.Id.Value}"));
        }
    }

    private sealed class NoProfile(string? choose = null) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
        {
            var choice = choose is null ? null : new PackChoice(choose);
            var profile = new SindriProfile(1, "t") { Workflow = new WorkflowSettings(Packs: new PackSettings(Lenses: new LensPackSettings(choice, choice))) };
            return ValueTask.FromResult<Result<LoadedProfile?>>(new LoadedProfile(profile, ProfileHash.From(new string('a', 64)), "/profile.yaml"));
        }
    }
}
