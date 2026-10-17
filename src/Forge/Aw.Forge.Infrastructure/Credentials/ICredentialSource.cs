using Aw.Kernel;

namespace Aw.Forge.Infrastructure.Credentials;

/// <summary>Hands out the forge token. Only Sindri's services and effects hold one; agent steps never do.</summary>
internal interface ICredentialSource
{
    /// <summary>The token for a host.</summary>
    /// <param name="refresh">True after a 401: drop what is cached and ask again.</param>
    public ValueTask<Result<string>> Token(string host, bool refresh, CancellationToken cancellationToken);
}
