using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Host;

/// <summary>
/// Wraps an environment function so that a name that is a secret pointer (context spec §4 "Secrets") resolves to the
/// secret, and any other name reads the real environment.
/// </summary>
internal static class SecretPointerEnvironment
{
    /// <summary>The wrapped environment function.</summary>
    /// <param name="environment">The real environment.</param>
    /// <param name="secrets">Resolves pointers.</param>
    /// <returns>A pointer's value, or null when it doesn't parse or resolve (never the real environment's value for that name); otherwise <paramref name="environment"/>'s.</returns>
    internal static Func<string, string?> Over(Func<string, string?> environment, ISecretPointers secrets)
        => name => !SecretPointer.IsPointer(name)
            ? environment(name)
            : SecretPointer.Parse(name).Bind(secrets.Resolve).Match<string?>(static value => value, static _ => null);
}
