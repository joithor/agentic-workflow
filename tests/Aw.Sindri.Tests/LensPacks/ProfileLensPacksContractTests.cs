using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.LensPacks;
using Aw.Sindri.Testing.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>The profile-driven lens pack resolver over in-memory pack files, held to the contract.</summary>
[Lane(TestLane.Unit)]
public sealed class ProfileLensPacksContractTests : LensPacksContract
{
    protected override ILensPacks Create(string? designDefault, string? reviewDefault, IReadOnlyDictionary<string, string> designRepos)
    {
        var lenses = new LensPackSettings(
            designDefault is null && designRepos.Count == 0 ? null : new PackChoice(designDefault, designRepos.Count == 0 ? null : designRepos),
            reviewDefault is null ? null : new PackChoice(reviewDefault));
        var profile = new SindriProfile(1, "t") { Workflow = new WorkflowSettings(Packs: new PackSettings(Lenses: lenses)) };
        return new ProfileLensPacks(new Profiles(new LoadedProfile(profile, ProfileHash.From(new string('a', 64)), "/profile.yaml")), new InMemoryPackSource(Available));
    }

    private sealed class Profiles(LoadedProfile? profile) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult<Result<LoadedProfile?>>(profile);
    }

    private sealed class InMemoryPackSource(IReadOnlyList<PackSpec> specs) : ILensPackSource
    {
        public IReadOnlyList<string> Ids() => [.. specs.Select(static s => s.Id).Order(StringComparer.Ordinal)];

        public IReadOnlyDictionary<string, string>? Files(string id)
        {
            if (specs.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.Ordinal)) is not { } spec)
            {
                return null;
            }

            var files = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pack.json"] = new JsonObject
                {
                    ["kind"] = "lenses",
                    ["id"] = spec.Id,
                    ["version"] = 1,
                    ["applies"] = spec.Applies == LensPackKind.Design ? "design" : "review",
                    ["lenses"] = new JsonArray([.. spec.LensIds().Select(static l => (JsonNode?)new JsonObject { ["id"] = l, ["title"] = l, ["prompt"] = $"lenses/{l}.md", ["when"] = "always" })]),
                }.ToJsonString(),
            };
            foreach (var lens in spec.LensIds())
            {
                files[$"lenses/{lens}.md"] = $"# {spec.Id} {lens}";
            }

            return files;
        }
    }
}
