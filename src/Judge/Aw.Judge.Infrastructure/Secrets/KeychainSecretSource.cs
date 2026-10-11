using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Secrets;

namespace Aw.Judge.Infrastructure.Secrets;

/// <summary>ISecretSource over the environment, then the macOS keychain.</summary>
/// <param name="environment">Reads the secret's variable.</param>
/// <param name="runner">Runs the keychain tool.</param>
internal sealed class KeychainSecretSource(Func<string, string?> environment, IProcessRunner runner) : ISecretSource
{
    /// <inheritdoc />
    public ValueTask<string?> Read(SecretName name, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"not implemented yet ({environment("PATH")}, {runner})");
}
