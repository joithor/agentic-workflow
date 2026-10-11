using System.Text.Json;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Visual;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Evidence.Infrastructure.Visual;

/// <summary>
/// The verdict cache as one JSON manifest, written through the package's AwJson options (Composition registers them)
/// and the run-file store, so a save replaces the manifest in one step. An unreadable or malformed manifest loads as
/// an empty cache: a cache is never worth failing a run.
/// </summary>
internal sealed class FileVerdictCacheStore(IRunFiles files, JsonSerializerOptions json) : IVerdictCacheStore
{
    /// <inheritdoc/>
    public async ValueTask<VerdictCache> LoadAsync(string manifestPath, CancellationToken cancellationToken)
    {
        var bytes = await files.ReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return VerdictCache.Empty;
        }

        var cache = AwJson.Deserialize<VerdictCache>(bytes.Value, json);
        return cache.IsError ? VerdictCache.Empty : cache.Value;
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> SaveAsync(string manifestPath, VerdictCache cache, CancellationToken cancellationToken) =>
        files.WriteAsync(manifestPath, AwJson.Serialize(cache, json), cancellationToken);
}
