using Aw.Kernel;

namespace Aw.Sindri.Core.LensPacks;

/// <summary>Resolves the pack an agent Step's 'bundle' parameter names into the files its Step directory gets (line spec §5.1, §5.2, LN-WF5).</summary>
public interface IPackBundles
{
    /// <summary>The pack files for a Step.</summary>
    /// <param name="request">What the Step asked for.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bundle; an error when the pack can't be resolved or pin-checked, the lens is not in the pack, or the bundle is neither 'style' nor 'lenses'.</returns>
    public ValueTask<Result<PackBundle>> Resolve(PackBundleRequest request, CancellationToken cancellationToken);
}
