using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>Renews an approval whose pinned blocks moved only by version (owner q93, 2026-10-23): the port the approval check calls, implemented where the registry's events are written.</summary>
internal interface IPinRenewals
{
    /// <summary>Renews <paramref name="standing"/> against <paramref name="build"/>.</summary>
    /// <param name="standing">The latest approval of the recipe.</param>
    /// <param name="build">This build's block pins for the recipe; they must be <see cref="PinRules.Compatible"/> with it.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>True when the approval now holds; an error when the renewal could not be written.</returns>
    public ValueTask<Result<bool>> Renew(RecipeApproval standing, IReadOnlyList<BlockPin> build, CancellationToken cancellationToken);
}
