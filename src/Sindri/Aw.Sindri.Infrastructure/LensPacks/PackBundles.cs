using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary>
/// <see cref="IPackBundles"/> over the style and lens pack resolvers (line spec §5.1, §5.2, LN-WF5). A style bundle is the resolved pack's own files. A lens bundle is the pack's 'pack.json'
/// (rewritten from the resolved pack, so a repo or machine pack that passed its pin check is described the same way as a built-in one) and the queued lens's prompt as 'prompt.md'.
/// </summary>
/// <param name="styles">Resolves the style pack.</param>
/// <param name="styleSource">The style packs' files.</param>
/// <param name="lenses">Resolves the lens pack.</param>
internal sealed class PackBundles(IStylePacks styles, IStylePackSource styleSource, ILensPacks lenses) : IPackBundles
{
    public async ValueTask<Result<PackBundle>> Resolve(PackBundleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Bundle switch
        {
            PackBundleNames.Style => await Style(request, cancellationToken).ConfigureAwait(false),
            PackBundleNames.Lenses => await Lenses(request, cancellationToken).ConfigureAwait(false),
            _ => SindriErrors.InvalidInput.With($"the bundle '{request.Bundle}' is neither '{PackBundleNames.Style}' nor '{PackBundleNames.Lenses}'"),
        };
    }

    private async ValueTask<Result<PackBundle>> Style(PackBundleRequest request, CancellationToken cancellationToken)
    {
        var pack = await styles.Resolve(request.Repo, cancellationToken).ConfigureAwait(false);
        if (pack.IsError)
        {
            return pack.Error;
        }

        return styleSource.Files(pack.Value.Id.Value) is { } files
            ? new PackBundle("style", pack.Value.Id, pack.Value.Hash, files)
            : PackErrors.NotFound.With($"the style pack '{pack.Value.Id.Value}' has no files to bundle");
    }

    private async ValueTask<Result<PackBundle>> Lenses(PackBundleRequest request, CancellationToken cancellationToken)
    {
        var kind = string.Equals(request.Subject, "change", StringComparison.Ordinal) ? LensPackKind.Review : LensPackKind.Design;
        var resolved = await lenses.Resolve(kind, request.Repo, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var pack = resolved.Value;
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { ["pack.json"] = Described(pack) };
        if (request.Lens is { } wanted)
        {
            if (pack.Lenses.FirstOrDefault(l => string.Equals(l.Id.Value, wanted, StringComparison.Ordinal)) is not { } lens)
            {
                return PackErrors.NotFound.With($"the lens '{wanted}' is not in the lens pack '{pack.Id.Value}' (it holds {string.Join(", ", pack.Lenses.Select(static l => l.Id.Value))})");
            }

            files["prompt.md"] = lens.Prompt;
        }

        return new PackBundle("lens", pack.Id, pack.Hash, files);
    }

    private static string Described(LensPack pack)
    {
        var lenses = new JsonArray();
        foreach (var lens in pack.Lenses)
        {
            lenses.Add((JsonNode)new JsonObject { ["id"] = lens.Id.Value, ["title"] = lens.Title });
        }

        return new JsonObject
        {
            ["kind"] = "lenses",
            ["id"] = pack.Id.Value,
            ["applies"] = pack.Applies == LensPackKind.Design ? "design" : "review",
            ["hash"] = pack.Hash,
            ["lenses"] = lenses,
        }.ToJsonString();
    }
}
