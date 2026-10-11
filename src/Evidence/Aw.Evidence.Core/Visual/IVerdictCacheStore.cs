using Aw.Kernel;

namespace Aw.Evidence.Core.Visual;

/// <summary>Persists the verdict cache in a manifest file.</summary>
public interface IVerdictCacheStore
{
    /// <summary>Loads the cache; a missing or unreadable manifest is an empty cache (a cache is never worth failing a run).</summary>
    /// <param name="manifestPath">The manifest file.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>The cache.</returns>
    public ValueTask<VerdictCache> LoadAsync(string manifestPath, CancellationToken cancellationToken);

    /// <summary>Saves the cache, replacing the manifest.</summary>
    /// <param name="manifestPath">The manifest file.</param>
    /// <param name="cache">The cache.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>Success, or AW-EVIDENCE-011.</returns>
    public ValueTask<Result<Unit>> SaveAsync(string manifestPath, VerdictCache cache, CancellationToken cancellationToken);
}
