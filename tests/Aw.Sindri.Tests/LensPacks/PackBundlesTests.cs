using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.LensPacks;
using Aw.Sindri.Testing.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>LN-WF5 (line spec §5.1, §5.2): the pack a Step bundles, resolved and pin-checked, as the files its Step directory gets and the {id, hash} the run record keeps.</summary>
[Lane(TestLane.Unit)]
public sealed class PackBundlesTests
{
    private const string Hash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PackBundles Bundles(FakeLensPacks? lenses = null)
    {
        var source = new EmbeddedStylePackSource();
        return new PackBundles(new ProfileStylePacks(new NoProfile(), source), source, lenses ?? new FakeLensPacks());
    }

    private static LensPack Design() => new(
        LensPackId.From("design-five"), Hash, LensPackKind.Design,
        [new PackLens(LensId.From("product"), "Product", "Look for scope creep.", LensWhen.Always), new PackLens(LensId.From("security"), "Security", "Look for threats.", LensWhen.Always)]);

    [Fact]
    public async Task The_style_bundle_is_the_resolved_packs_files_in_style_with_its_id_and_hash()
    {
        var bundle = await Bundles().Resolve(new PackBundleRequest("style", Repo: null, Subject: null, Lens: null), Token);

        Assert.False(bundle.IsError);
        Assert.Equal("style", bundle.Value.Directory);
        Assert.Equal(LensPackId.From("plain"), bundle.Value.PackId);
        Assert.Equal(new ProfileStylePacks(new NoProfile(), new EmbeddedStylePackSource()).Find(LensPackId.From("plain")).Value.Hash, bundle.Value.Hash);
        Assert.Contains("pack.json", bundle.Value.Files.Keys, StringComparer.Ordinal);
        Assert.Contains("template.md", bundle.Value.Files.Keys, StringComparer.Ordinal);
        Assert.Contains("voice.md", bundle.Value.Files.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_lens_bundle_holds_the_packs_pack_json_and_the_queued_lens_prompt()
    {
        var bundles = Bundles(new FakeLensPacks().With(Design()));

        var bundle = await bundles.Resolve(new PackBundleRequest("lenses", Repo: null, Subject: "design", Lens: "security"), Token);

        Assert.False(bundle.IsError);
        Assert.Equal("lens", bundle.Value.Directory);
        Assert.Equal(LensPackId.From("design-five"), bundle.Value.PackId);
        Assert.Equal(Hash, bundle.Value.Hash);
        Assert.Equal(["pack.json", "prompt.md"], bundle.Value.Files.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal("Look for threats.", bundle.Value.Files["prompt.md"]);
        Assert.Contains("\"id\":\"design-five\"", bundle.Value.Files["pack.json"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_lens_bundle_with_no_queued_lens_holds_pack_json_alone()
    {
        var bundle = await Bundles(new FakeLensPacks().With(Design())).Resolve(new PackBundleRequest("lenses", Repo: null, Subject: "design", Lens: null), Token);

        Assert.Equal(["pack.json"], bundle.Value.Files.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_change_subject_asks_for_the_review_pack()
    {
        var packs = new FakeLensPacks().With(new LensPack(LensPackId.From("code-review"), Hash, LensPackKind.Review, [new PackLens(LensId.From("correctness"), "Correctness", "Look for bugs.", LensWhen.Always)]));

        var bundle = await Bundles(packs).Resolve(new PackBundleRequest("lenses", RepoName.From("web"), Subject: "change", Lens: "correctness"), Token);

        Assert.Equal(LensPackId.From("code-review"), bundle.Value.PackId);
        var (kind, repo) = Assert.Single(packs.Asked);
        Assert.Equal(LensPackKind.Review, kind);
        Assert.Equal("web", repo?.Value);
    }

    [Fact]
    public async Task A_lens_that_is_not_in_the_pack_is_an_error_naming_it()
    {
        var bundle = await Bundles(new FakeLensPacks().With(Design())).Resolve(new PackBundleRequest("lenses", Repo: null, Subject: "design", Lens: "nope"), Token);

        Assert.True(bundle.IsError);
        Assert.Contains("nope", bundle.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pack_that_does_not_resolve_is_the_resolvers_own_error()
    {
        var bundle = await Bundles().Resolve(new PackBundleRequest("lenses", Repo: null, Subject: "design", Lens: null), Token);

        Assert.True(bundle.IsError);
        Assert.Contains("design-five", bundle.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bundle_that_is_neither_style_nor_lenses_is_refused()
    {
        var bundle = await Bundles().Resolve(new PackBundleRequest("other", Repo: null, Subject: null, Lens: null), Token);

        Assert.True(bundle.IsError);
        Assert.Contains("'other'", bundle.Error.Message, StringComparison.Ordinal);
    }

    private sealed class NoProfile : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
        {
            var profile = new SindriProfile(1, "t");
            return ValueTask.FromResult<Result<LoadedProfile?>>(new LoadedProfile(profile, ProfileHash.From(new string('a', 64)), "/profile.yaml"));
        }
    }
}
