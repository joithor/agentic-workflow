using System.Text.Json;

namespace Aw.Evidence.Core.Visual;

/// <summary>What a cached verdict is keyed on: the hashes of the actual pixels plus their context.</summary>
/// <param name="AfterHash">SHA-256 of the screenshot under review.</param>
/// <param name="BaselineHash">SHA-256 of the baseline, when there is one.</param>
/// <param name="Context">The verdict's context.</param>
public sealed record VerdictCacheKey(string AfterHash, string? BaselineHash, VerdictCacheContext Context)
{
    /// <summary>The cache key: SHA-256 of this record written through the package's AwJson options.</summary>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>64 lower-case hex characters.</returns>
    public string Compute(JsonSerializerOptions options) => ContentHash.Sha256Hex(this, options);
}
