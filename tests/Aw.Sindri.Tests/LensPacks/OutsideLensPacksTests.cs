using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>Packs a repo or the machine supplies (line spec §5.1): repo, then machine, then built in; one that is not built in needs a pin in 'workflow.packs.pins'.</summary>
[Lane(TestLane.Unit)]
public sealed class OutsideLensPacksTests
{
    private const string RepoPacks = "/work/web/.agentic-workflow/packs";
    private const string MachinePacks = "/state/packs";

    private readonly InMemoryPackFiles _files = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RepoName Web => RepoName.From("web");

    private static Dictionary<string, string> Pack(string id, string lensId) => new(StringComparer.Ordinal)
    {
        ["pack.json"] = $$"""{"kind":"lenses","id":"{{id}}","version":1,"applies":"design","lenses":[{"id":"{{lensId}}","title":"Lens","prompt":"lenses/{{lensId}}.md","when":"always"}]}""",
        [$"lenses/{lensId}.md"] = "look for " + lensId,
    };

    private void Put(string root, string id, string lensId)
    {
        foreach (var (path, text) in Pack(id, lensId))
        {
            _files.Put(root, id, path, text);
        }
    }

    private static string HashOf(string id, string lensId) => LensPackReader.Read(LensPackId.From(id), Pack(id, lensId)).Value.Hash;

    private ProfileLensPacks Packs(string chosen, Dictionary<string, string>? pins = null, bool repoEntry = true)
    {
        var choice = new PackChoice(chosen);
        var profile = new SindriProfile(1, "t")
        {
            Repos = repoEntry ? [new RepoEntry("web", "/work/web")] : null,
            Workflow = new WorkflowSettings(Packs: new PackSettings(Lenses: new LensPackSettings(choice, choice), Pins: pins)),
        };
        return new ProfileLensPacks(new Profiles(profile), new EmbeddedLensPackSource(), _files, MachinePacks);
    }

    [Fact]
    public async Task A_pinned_repo_pack_resolves_for_that_repo()
    {
        Put(RepoPacks, "house", "taste");

        var pack = await Packs("house", new(StringComparer.Ordinal) { ["lenses/house"] = HashOf("house", "taste") }).Resolve(LensPackKind.Design, Web, Token);

        Assert.Equal("taste", Assert.Single(pack.Value.Lenses).Id.Value);
    }

    [Fact]
    public async Task A_pinned_machine_pack_resolves_when_the_repo_has_none()
    {
        Put(MachinePacks, "house", "taste");

        var pack = await Packs("house", new(StringComparer.Ordinal) { ["lenses/house"] = HashOf("house", "taste") }).Resolve(LensPackKind.Design, Web, Token);

        Assert.Equal("taste", Assert.Single(pack.Value.Lenses).Id.Value);
    }

    [Fact]
    public async Task The_repo_pack_wins_over_the_machine_pack_of_the_same_id()
    {
        Put(MachinePacks, "house", "machine-lens");
        Put(RepoPacks, "house", "repo-lens");

        var pack = await Packs("house", new(StringComparer.Ordinal) { ["lenses/house"] = HashOf("house", "repo-lens") }).Resolve(LensPackKind.Design, Web, Token);

        Assert.Equal("repo-lens", Assert.Single(pack.Value.Lenses).Id.Value);
    }

    [Fact]
    public async Task A_repo_pack_is_not_seen_by_another_repo_or_none()
    {
        Put(RepoPacks, "house", "taste");

        var pack = await Packs("house", new(StringComparer.Ordinal) { ["lenses/house"] = HashOf("house", "taste") }).Resolve(LensPackKind.Design, repo: null, Token);

        Assert.Equal("SND-PACK-001", pack.Error.Code);
    }

    [Fact]
    public async Task A_pack_with_no_pin_is_SND_PACK_002_and_names_the_pin_line_to_approve()
    {
        Put(RepoPacks, "house", "taste");

        var pack = await Packs("house").Resolve(LensPackKind.Design, Web, Token);

        Assert.Equal("SND-PACK-002", pack.Error.Code);
        Assert.Contains("lenses/house: " + HashOf("house", "taste"), pack.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_pack_whose_hash_differs_from_its_pin_is_SND_PACK_002()
    {
        Put(MachinePacks, "house", "taste");

        var pack = await Packs("house", new(StringComparer.Ordinal) { ["lenses/house"] = "sha256:" + new string('0', 64) }).Resolve(LensPackKind.Design, Web, Token);

        Assert.Equal("SND-PACK-002", pack.Error.Code);
    }

    [Fact]
    public async Task A_built_in_pack_needs_no_pin_and_a_repo_pack_of_its_id_is_still_pinned()
    {
        Assert.True((await Packs("design-five").Resolve(LensPackKind.Design, Web, Token)).IsSuccess);

        Put(RepoPacks, "design-five", "taste");

        Assert.Equal("SND-PACK-002", (await Packs("design-five").Resolve(LensPackKind.Design, Web, Token)).Error.Code);
    }

    private sealed class Profiles(SindriProfile profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<LoadedProfile?>>(new LoadedProfile(profile, ProfileHash.From(new string('a', 64)), "/profile.yaml"));
    }
}
