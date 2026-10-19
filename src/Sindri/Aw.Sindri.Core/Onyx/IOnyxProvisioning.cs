using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// The setup-only requests of spec §5.2, used by `sindri onyx up` in the human's process and by nothing else. Its allowlist is separate from the query adapter's (which stays at five requests);
/// neither type uses the other. A request off the list is refused before it is sent. The login cookie lives in the session object only.
/// </summary>
public interface IOnyxProvisioning
{
    /// <summary>Logs the owner in (`POST /api/auth/login`, a form).</summary>
    /// <param name="port">The loopback port.</param>
    /// <param name="email">The owner's Onyx email.</param>
    /// <param name="password">The owner's Onyx password; used for this call and not kept.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The session, or SND-ONYX-008 when the login is refused.</returns>
    public ValueTask<Result<IOnyxSession>> SignIn(int port, string email, string password, CancellationToken cancellationToken);

    /// <summary>Asks whether a key is an admin service account (`GET /api/me` with the key).</summary>
    /// <param name="port">The loopback port.</param>
    /// <param name="key">The API key.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True for an admin service account, false when the key is refused or isn't an admin; an error when Onyx can't be reached.</returns>
    public ValueTask<Result<bool>> KeyIsAdmin(int port, string key, CancellationToken cancellationToken);
}
