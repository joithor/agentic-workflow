using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>
/// A logged-in Onyx session; disposing it logs out and drops the cookie. Two kinds of login use it: the owner's (an admin), who sets up Sindri's own Onyx user, and that user's, who revokes and mints its token.
/// Each call is one Community Edition route that Onyx's license tiers leave open (docs/sindri/onyx.md section 7); an expected refusal comes back as an error, never as an exception.
/// The calls that change a user are pinned to Sindri's dedicated user (and the one retired address): none takes an email, so no caller can point them at the owner's own account or anyone else's.
/// </summary>
public interface IOnyxSession : IAsyncDisposable
{
    /// <summary>Deactivates the retired dedicated user of an earlier build, if Onyx has one (`PATCH /api/manage/admin/deactivate-user`; a deactivated user's tokens stop working).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>True when a user was deactivated, false when Onyx has none; SND-ONYX-007 when the login isn't an admin.</returns>
    public ValueTask<Result<bool>> RetireLegacyUser(CancellationToken cancellationToken);

    /// <summary>Gives the dedicated user a new random password (`POST /api/password/reset_password`, an admin's route, which needs no invite and so works on an invite-only Onyx).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new password, which only the caller holds (never print or store it); null when Onyx has no such user yet; SND-ONYX-007 when the login isn't an admin.</returns>
    public ValueTask<Result<string?>> ResetDedicatedPassword(CancellationToken cancellationToken);

    /// <summary>Registers the dedicated user (`POST /api/auth/register`), which Onyx allows unless the workspace is invite-only or restricts email domains.</summary>
    /// <param name="password">The password it gets; used for this call and not kept.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Success, or SND-ONYX-008 when Onyx refuses (invite-only, a domain list, a password it won't take).</returns>
    public ValueTask<Result<Unit>> RegisterDedicatedUser(string password, CancellationToken cancellationToken);

    /// <summary>Makes the dedicated user an admin (`PATCH /api/manage/admin/users/admin-access`, `is_admin: true`; already an admin is a success).</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Success, SND-ONYX-007 when the login isn't an admin, or SND-ONYX-008 when the user doesn't exist.</returns>
    public ValueTask<Result<Unit>> MakeDedicatedAdmin(CancellationToken cancellationToken);

    /// <summary>Revokes every personal access token of the logged-in user that has this name (`GET /api/user/pats`, then `DELETE /api/user/pats/{id}` for each), so after the next mint exactly one is left.</summary>
    /// <param name="name">The token name (`sindri-onyx`).</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>How many were revoked, or SND-ONYX-008.</returns>
    public ValueTask<Result<int>> RevokeTokens(string name, CancellationToken cancellationToken);

    /// <summary>Makes a personal access token for the logged-in user (`POST /api/user/pats`, no expiry, no scopes: it carries the user's full permissions); the raw token is returned once.</summary>
    /// <param name="name">The token's name (`sindri-onyx`).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The raw token (`onyx_pat_…`), or SND-ONYX-007 or SND-ONYX-008.</returns>
    public ValueTask<Result<string>> CreateToken(string name, CancellationToken cancellationToken);
}
