using Aw.Kernel;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Infrastructure.Secrets;

/// <summary>ISecretPointers over the environment, owner-only files, the macOS keychain and the 1Password CLI.</summary>
/// <param name="environment">Reads an env: pointer's variable.</param>
/// <param name="runner">Runs security and op.</param>
internal sealed class PointerSecrets(Func<string, string?> environment, IToolRunner runner) : ISecretPointers
{
    /// <inheritdoc />
    public Result<string> Resolve(SecretPointer secretPointer) => throw new NotSupportedException($"red: resolve {secretPointer} ({environment.Method.Name}, {runner.GetType().Name})");
}
