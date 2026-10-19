using Aw.Context.Core.Onyx;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>
/// Spec §4.1 steps 7 and 8: Sindri's own Onyx user and its token. Onyx Community Edition has no service-account keys (their route is Business tier), so Sindri gets a dedicated admin user (<see cref="OnyxSettings.DedicatedAccountEmail"/>)
/// and a personal access token of that user. The owner's Onyx login is asked for once at the terminal, held in memory for the calls below and dropped; with it the user is registered (or, when it already exists, given a new random password)
/// and made an admin; then Sindri logs in as that user with the password it just made, mints an unscoped token with no expiry, checks it and stores it, and only a pointer to it is returned. The user's password is random, lives in memory
/// for this one call and is never stored or printed. Every step is safe to repeat: a rerun after any failure finds the user and resets its password, so no second user and no second sign-up are needed.
/// A recorded token is trusted again only when it still resolves from where it was stored and Onyx still takes it as that user's admin token.
/// </summary>
internal sealed class OnyxKeyMinter(IOnyxPrompts prompts, IOnyxProvisioning provisioning, ISecretWriter secrets, ISecretPointers pointers, IRandomHex random)
{
    private const string KeyName = "sindri-onyx";
    private const string KeyService = "onyx";

    /// <summary>What the owner is told whenever a token named sindri-onyx may be left in Onyx with nobody holding it (the allowlist has no revoke request, and the user's password isn't kept).</summary>
    internal const string LeftoverNote = "A personal access token named sindri-onyx may still exist on Onyx's user sindri@sindri.app with nobody holding it; it is harmless, and a rerun of `sindri onyx up` makes a new one.";

    internal async ValueTask<bool> Stands(int port, string? pointer, CancellationToken cancellationToken)
    {
        if (pointer is null || SecretPointer.Parse(pointer) is not { IsSuccess: true } parsed)
        {
            return false;
        }

        var key = pointers.Resolve(parsed.Value);
        if (key.IsError)
        {
            return false;
        }

        var admin = await provisioning.TokenIsAdmin(port, key.Value, OnyxSettings.DedicatedAccountEmail, cancellationToken).ConfigureAwait(false);
        return admin is { IsSuccess: true, Value: true };
    }

    /// <summary>Makes the dedicated user and its token, and stores the token.</summary>
    /// <param name="port">Onyx's loopback port.</param>
    /// <param name="cancellationToken">Cancels the asking and the requests.</param>
    /// <returns>The pointer to the stored token, or SND-ONYX-007, 008 or 009.</returns>
    internal async ValueTask<Result<string>> Mint(int port, CancellationToken cancellationToken)
    {
        var password = await Prepare(port, cancellationToken).ConfigureAwait(false);
        if (password.IsError)
        {
            return password.Error;
        }

        var minted = await Token(port, password.Value, cancellationToken).ConfigureAwait(false);
        if (minted.IsError)
        {
            return minted.Error;
        }

        var stored = secrets.Store(KeyService, KeyName, minted.Value);
        return stored.IsError ? Leftover(stored.Error) : stored.Value.ToString();
    }

    // The owner's session: the dedicated user exists, has a password only this call knows, and is an admin.
    private async ValueTask<Result<string>> Prepare(int port, CancellationToken cancellationToken)
    {
        var email = await prompts.Ask("Your Onyx email: ", cancellationToken).ConfigureAwait(false);
        var password = email is { Length: > 0 } ? await prompts.AskSecret("Your Onyx password (not shown, not stored): ", cancellationToken).ConfigureAwait(false) : null;
        if (email is not { Length: > 0 } || password is not { Length: > 0 })
        {
            return SindriErrors.OnyxKeyFailed.With("no Onyx login was given; run `sindri onyx up` again and enter the email and password of the account you created");
        }

        var signedIn = await provisioning.SignIn(port, email, password, cancellationToken).ConfigureAwait(false);
        if (signedIn.IsError)
        {
            return signedIn.Error;
        }

        var session = signedIn.Value;
        await using (session.ConfigureAwait(false))   // logs out and drops the cookie
        {
            var own = NewPassword();
            var created = await session.RegisterUser(OnyxSettings.DedicatedAccountEmail, own, cancellationToken).ConfigureAwait(false);
            if (created.IsError)
            {
                return created.Error;
            }

            var current = created.Value ? own : await session.ResetPassword(OnyxSettings.DedicatedAccountEmail, cancellationToken).ConfigureAwait(false);
            if (current.IsError)
            {
                return current.Error;
            }

            var admin = await session.MakeAdmin(OnyxSettings.DedicatedAccountEmail, cancellationToken).ConfigureAwait(false);
            return admin.IsError ? admin.Error : current;
        }
    }

    // The dedicated user's own session: log in with the password just made, mint the token, check it. The token exists in Onyx from the moment it is made.
    private async ValueTask<Result<string>> Token(int port, string password, CancellationToken cancellationToken)
    {
        var signedIn = await provisioning.SignIn(port, OnyxSettings.DedicatedAccountEmail, password, cancellationToken).ConfigureAwait(false);
        if (signedIn.IsError)
        {
            return SindriErrors.OnyxKeyFailed.With($"Onyx didn't let Sindri's own user {OnyxSettings.DedicatedAccountEmail} log in right after setting it up: {signedIn.Error.Message}");
        }

        var session = signedIn.Value;
        await using (session.ConfigureAwait(false))
        {
            var made = await session.CreateToken(KeyName, cancellationToken).ConfigureAwait(false);
            if (made.IsError)
            {
                return made;
            }

            var verified = await Verified(port, made.Value, cancellationToken).ConfigureAwait(false);
            return verified.IsError ? Leftover(verified.Error) : verified;
        }
    }

    // Random enough to be unguessable, and shaped to pass whatever password rules the owner turned on: lower, upper, digit and special character, at most 64 long.
    private string NewPassword() => random.Hex(16) + "aA1!";

    private static Error Leftover(Error error) => error with { Message = $"{error.Message}. {LeftoverNote}" };

    private async ValueTask<Result<string>> Verified(int port, string token, CancellationToken cancellationToken)
    {
        var admin = await provisioning.TokenIsAdmin(port, token, OnyxSettings.DedicatedAccountEmail, cancellationToken).ConfigureAwait(false);
        if (admin.IsError)
        {
            return admin.Error;
        }

        return admin.Value
            ? token
            : SindriErrors.OnyxKeyFailed.With("Onyx made the token but doesn't treat it as the admin token of Sindri's user; check that user in Onyx's admin users page, then run `sindri onyx up` again");
    }
}
