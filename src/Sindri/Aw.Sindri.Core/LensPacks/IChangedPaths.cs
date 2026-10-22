using Aw.Kernel;

namespace Aw.Sindri.Core.LensPacks;

/// <summary>The paths a change touches, for the review loop's lens selection (line spec §5.2: 'the changed paths').</summary>
public interface IChangedPaths
{
    /// <summary>The paths that differ between a change's base and its head.</summary>
    /// <param name="checkout">The repo's checkout.</param>
    /// <param name="baseRef">The change's base (a commit or branch).</param>
    /// <param name="head">The change's head (a commit or branch).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The paths relative to the repo root, '/' separated, in path order and without duplicates; an error when git can't compare the two.</returns>
    public ValueTask<Result<IReadOnlyList<string>>> Between(string checkout, string baseRef, string head, CancellationToken cancellationToken);
}
