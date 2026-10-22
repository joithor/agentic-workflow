using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Testing.LensPacks;

namespace Aw.Sindri.Tests.LensPacks;

/// <summary>The in-memory lens pack resolver, held to the same contract as the profile adapter.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeLensPacksContractTests : LensPacksContract
{
    protected override ILensPacks Create(string? designDefault, string? reviewDefault, IReadOnlyDictionary<string, string> designRepos)
    {
        var fake = new FakeLensPacks();
        foreach (var spec in Available)
        {
            _ = fake.With(new LensPack(LensPackId.From(spec.Id), "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(spec.Id))), spec.Applies, [.. spec.LensIds().Select(static id => new PackLens(LensId.From(id), id, "prompt", LensWhen.Always))]));
        }

        if (designDefault is not null)
        {
            _ = fake.Choosing(LensPackKind.Design, designDefault);
        }

        if (reviewDefault is not null)
        {
            _ = fake.Choosing(LensPackKind.Review, reviewDefault);
        }

        foreach (var (repo, id) in designRepos)
        {
            _ = fake.ChoosingFor(LensPackKind.Design, repo, id);
        }

        return fake;
    }
}
