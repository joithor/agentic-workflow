using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.LensPacks;

/// <summary>
/// Resolves the lens pack a loop uses (line spec §5.2, LN-PK lite): the approved profile's <c language="text">workflow.packs.lenses.design</c> or
/// <c language="text">.review</c> chooses the pack id (a repo's entry wins over the default; <c language="text">design-five</c> and <c language="text">code-review</c>
/// when it names none), and the pack is a built-in one. Repo and machine packs and pins are a later unit.
/// </summary>
public interface ILensPacks
{
    /// <summary>The pack the profile chooses for the loop.</summary>
    /// <param name="kind">The loop.</param>
    /// <param name="repo">The repo the instance works on, for the profile's per-repo entry; null for none.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The pack; an error when the chosen id is not a built-in pack, the pack serves the other loop, or it holds no lens or more than <see cref="LensPack.MaxLenses"/>.</returns>
    public ValueTask<Result<LensPack>> Resolve(LensPackKind kind, RepoName? repo, CancellationToken cancellationToken);
}
