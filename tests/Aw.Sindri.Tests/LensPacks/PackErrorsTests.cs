using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Infrastructure.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>The SND-PACK codes (line spec Appendix D): registered with the other catalogues, and the pack resolver and reader answer with them.</summary>
[Lane(TestLane.Unit)]
public sealed class PackErrorsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void The_pack_catalogue_is_valid_together_with_the_others() =>
        Assert.True(ErrorCatalogue.Validate([.. SindriErrors.All, .. WorkflowErrors.All, .. ScopingErrors.All, .. LineErrors.All, .. PackErrors.All]).IsSuccess);

    [Theory]
    [InlineData("SND-PACK-001", ErrorKind.NotFound)]
    [InlineData("SND-PACK-002", ErrorKind.Conflict)]
    [InlineData("SND-PACK-003", ErrorKind.Invalid)]
    [InlineData("SND-PACK-004", ErrorKind.Invalid)]
    public void Each_code_has_its_kind(string code, ErrorKind kind) =>
        Assert.Equal(kind, Assert.Single(PackErrors.All, e => e.Code == code).Kind);

    [Fact]
    public async Task A_pack_id_that_resolves_to_nothing_is_SND_PACK_001()
    {
        var resolved = await Packs("no-such-pack").Resolve(LensPackKind.Design, repo: null, Token);

        Assert.Equal("SND-PACK-001", resolved.Error.Code);
    }

    [Fact]
    public async Task A_pack_that_serves_the_other_loop_is_SND_PACK_004()
    {
        var resolved = await Packs("code-review").Resolve(LensPackKind.Design, repo: null, Token);

        Assert.Equal("SND-PACK-004", resolved.Error.Code);
    }

    [Fact]
    public void More_than_ten_lenses_is_SND_PACK_003()
    {
        var read = LensPackReader.Read(LensPackId.From("big-pack"), PackFiles(lenses: 11));

        Assert.Equal("SND-PACK-003", read.Error.Code);
    }

    [Fact]
    public void A_missing_prompt_file_is_SND_PACK_003()
    {
        var files = PackFiles(lenses: 2);
        _ = files.Remove("lenses/l1.md");

        Assert.Equal("SND-PACK-003", LensPackReader.Read(LensPackId.From("big-pack"), files).Error.Code);
    }

    private static ProfileLensPacks Packs(string chosen) => new(new Chosen(chosen), new EmbeddedLensPackSource());

    private static Dictionary<string, string> PackFiles(int lenses)
    {
        var entries = string.Join(',', Enumerable.Range(0, lenses).Select(static i => $$"""{"id":"l{{i.ToString(CultureInfo.InvariantCulture)}}","title":"Lens {{i.ToString(CultureInfo.InvariantCulture)}}","prompt":"lenses/l{{i.ToString(CultureInfo.InvariantCulture)}}.md","when":"always"}"""));
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pack.json"] = $$"""{"kind":"lenses","id":"big-pack","version":1,"applies":"design","lenses":[{{entries}}]}""",
        };
        for (var i = 0; i < lenses; i++)
        {
            files[$"lenses/l{i.ToString(CultureInfo.InvariantCulture)}.md"] = "look for " + Encoding.UTF8.GetString([(byte)('a' + i)]);
        }

        return files;
    }

    private sealed class Chosen(string id) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
        {
            var choice = new PackChoice(id);
            var profile = new SindriProfile(1, "t") { Workflow = new WorkflowSettings(Packs: new PackSettings(Lenses: new LensPackSettings(choice, choice))) };
            return ValueTask.FromResult<Result<LoadedProfile?>>(new LoadedProfile(profile, ProfileHash.From(new string('a', 64)), "/profile.yaml"));
        }
    }
}
