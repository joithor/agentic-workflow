using Aw.Forge.Core;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.Credentials;

/// <summary>A long-lived token in an environment variable (env:AW_FORGE_TOKEN).</summary>
internal sealed class EnvCredentialSource(string name, Func<string, string?> environment) : ICredentialSource
{
    public ValueTask<Result<string>> Token(string host, bool refresh, CancellationToken cancellationToken) =>
        ValueTask.FromResult(environment(name) is { Length: > 0 } token
            ? Results.Success(token.Trim())
            : Results.Failure<string>(ForgeErrors.Unauthorized.With($"{name} is not set")));
}
