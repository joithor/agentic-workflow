using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.LensPacks;

/// <summary>
/// Resolves the style pack a design is written in (line spec §5.1): the approved profile's <c language="text">workflow.packs.style</c> chooses the id (a repo's entry wins over the default;
/// <c language="text">plain</c> when it names none), and the pack is a built-in one. Repo and machine packs and pins are a later unit.
/// </summary>
public interface IStylePacks
{
    /// <summary>The pack the profile chooses.</summary>
    /// <param name="repo">The repo the instance works on, for the profile's per-repo entry; null for none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The pack; an error when the chosen id is not a built-in pack or its files are not a style pack.</returns>
    public ValueTask<Result<StylePack>> Resolve(RepoName? repo, CancellationToken cancellationToken);

    /// <summary>A built-in pack by id, for a check that reads the id a run pinned.</summary>
    /// <param name="id">The pack id.</param>
    /// <returns>The pack; an error when there is no such pack.</returns>
    public Result<StylePack> Find(LensPackId id);
}
