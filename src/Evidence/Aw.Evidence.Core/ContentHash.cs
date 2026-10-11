using System.Security.Cryptography;
using System.Text.Json;
using Aw.Kernel.Json;

namespace Aw.Evidence.Core;

/// <summary>SHA-256 digests as lower-case hex.</summary>
public static class ContentHash
{
    /// <summary>The digest of <paramref name="content"/>.</summary>
    /// <param name="content">The bytes.</param>
    /// <returns>64 lower-case hex characters.</returns>
    public static string Sha256Hex(ReadOnlySpan<byte> content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>The digest of <paramref name="value"/> written through the package's AwJson options.</summary>
    /// <param name="value">A value the options' context knows.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <returns>64 lower-case hex characters.</returns>
    public static string Sha256Hex<T>(T value, JsonSerializerOptions options) => Sha256Hex(AwJson.Serialize(value, options));
}
