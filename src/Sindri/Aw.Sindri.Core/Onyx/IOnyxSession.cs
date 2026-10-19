using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// A logged-in Onyx session; disposing it logs out and drops the cookie. Two kinds of login use it: the owner's (an admin), who sets up Sindri's own Onyx user, and that user's, who mints its token.
/// Each call is one Community Edition route that Onyx's license tiers leave open (docs/sindri/onyx.md section 7); an expected refusal comes back as an error, never as an exception.
/// </summary>
public interface IOnyxSession : IAsyncDisposable
{
    /// <summary>Registers a local user (`POST /api/auth/register`, open unless the owner made the workspace invite-only).</summary>
    /// <param name="email">The user's email.</param>
    /// <param name="password">The password it gets; used for this call and not kept.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when the user was made, false when Onyx says the email is already registered; SND-ONYX-008 when Onyx refuses (invite-only, a password it won't take).</returns>
    public ValueTask<Result<bool>> RegisterUser(string email, string password, CancellationToken cancellationToken);

    /// <summary>Gives an existing user a new random password (`POST /api/password/reset_password`, an admin's route).</summary>
    /// <param name="email">The user's email.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new password, which only the caller holds (never print or store it); SND-ONYX-007 when the login isn't an admin, SND-ONYX-008 when the user doesn't exist.</returns>
    public ValueTask<Result<string>> ResetPassword(string email, CancellationToken cancellationToken);

    /// <summary>Makes a user an admin (`PATCH /api/manage/admin/users/admin-access`, `{user_email, is_admin: true}`; already an admin is a success).</summary>
    /// <param name="email">The user's email.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Success, SND-ONYX-007 when the login isn't an admin, or SND-ONYX-008 when the user doesn't exist.</returns>
    public ValueTask<Result<Unit>> MakeAdmin(string email, CancellationToken cancellationToken);

    /// <summary>Makes a personal access token for the logged-in user (`POST /api/user/pats`, no expiry, no scopes: it carries the user's full permissions); the raw token is returned once.</summary>
    /// <param name="name">The token's name (`sindri-onyx`).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The raw token (`onyx_pat_…`), or SND-ONYX-007 or SND-ONYX-008.</returns>
    public ValueTask<Result<string>> CreateToken(string name, CancellationToken cancellationToken);
}
