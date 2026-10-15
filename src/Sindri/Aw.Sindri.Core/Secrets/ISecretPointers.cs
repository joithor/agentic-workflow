using Aw.Kernel;

namespace Aw.Sindri.Core.Secrets;

/// <summary>
/// Resolves a secret pointer to its value (context spec §4 "Secrets"). Synchronous on purpose: Sindri Host calls it through
/// the 'Func&lt;string, string?&gt;' environment function it passes to a package's 'Add…Infrastructure', which
/// has no async form. The keychain and 1Password paths run a tool and may block up to its timeout (5 s and 30 s).
/// </summary>
public interface ISecretPointers
{
    /// <summary>Reads the secret the pointer names. Never falls back to any other source.</summary>
    /// <param name="secretPointer">The parsed pointer.</param>
    /// <returns>The value; or SND-SECRET-002 when it doesn't resolve, SND-SECRET-003 when its file is unsafe.</returns>
    public Result<string> Resolve(SecretPointer secretPointer);
}
