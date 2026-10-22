using Aw.Kernel;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Stores the one secret Sindri writes (Onyx's personal access token) where `SecretPointer` can read it back. The secret never appears in an argument list, a log or an error.</summary>
public interface ISecretWriter
{
    /// <summary>Stores a secret.</summary>
    /// <param name="service">The keychain service (`onyx`).</param>
    /// <param name="account">The keychain account (`sindri-onyx`).</param>
    /// <param name="secret">The secret.</param>
    /// <returns>The pointer to put in the profile (`keychain:onyx/sindri-onyx`, or a `file:` pointer when no store is available), or SND-ONYX-009.</returns>
    public Result<SecretPointer> Store(string service, string account, string secret);
}
