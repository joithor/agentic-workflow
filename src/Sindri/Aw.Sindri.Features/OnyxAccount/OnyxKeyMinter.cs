using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>
/// Spec §4.1 steps 7 and 8: Sindri's admin key. The owner's Onyx login is asked for once at the terminal, held in memory for the calls below and dropped; the key is made, checked and stored, and only a pointer to it is returned.
/// A recorded key is trusted again only when it still resolves from where it was stored and Onyx still takes it as an admin service account.
/// </summary>
internal sealed class OnyxKeyMinter(IOnyxPrompts prompts, IOnyxProvisioning provisioning, ISecretWriter secrets, ISecretPointers pointers, OnyxAdminGroup adminGroup)
{
    private const string KeyName = "sindri-onyx";
    private const string KeyService = "onyx";

    /// <summary>What the owner is told whenever a key named sindri-onyx may be left in Onyx with nobody holding it (the allowlist has no delete request).</summary>
    internal const string LeftoverNote = "An API key named sindri-onyx may still be in Onyx and is not in use; delete it on Onyx's admin API keys page.";

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

        var admin = await provisioning.KeyIsAdmin(port, key.Value, cancellationToken).ConfigureAwait(false);
        return admin is { IsSuccess: true, Value: true };
    }

    /// <summary>Makes the key and stores it.</summary>
    /// <param name="port">Onyx's loopback port.</param>
    /// <param name="cancellationToken">Cancels the asking and the requests.</param>
    /// <returns>The pointer to the stored key, or SND-ONYX-007, 008 or 009.</returns>
    internal async ValueTask<Result<string>> Mint(int port, CancellationToken cancellationToken)
    {
        var minted = await Request(port, cancellationToken).ConfigureAwait(false);
        if (minted.IsError)
        {
            return minted.Error;
        }

        var stored = secrets.Store(KeyService, KeyName, minted.Value);
        return stored.IsError ? Leftover(stored.Error) : stored.Value.ToString();
    }

    private async ValueTask<Result<string>> Request(int port, CancellationToken cancellationToken)
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
            var group = await adminGroup.Find(session, cancellationToken).ConfigureAwait(false);
            var made = group.IsError ? group.Error : await session.CreateKey(KeyName, group.Value, cancellationToken).ConfigureAwait(false);
            if (made.IsError)
            {
                return made;
            }

            var verified = await Verified(port, made.Value, cancellationToken).ConfigureAwait(false);
            return verified.IsError ? Leftover(verified.Error) : verified;   // the key exists in Onyx from here on
        }
    }

    private static Error Leftover(Error error) => error with { Message = $"{error.Message}. {LeftoverNote}" };

    private async ValueTask<Result<string>> Verified(int port, string key, CancellationToken cancellationToken)
    {
        var admin = await provisioning.KeyIsAdmin(port, key, cancellationToken).ConfigureAwait(false);
        if (admin.IsError)
        {
            return admin.Error;
        }

        return admin.Value
            ? key
            : SindriErrors.OnyxKeyFailed.With("Onyx made the key but doesn't treat it as an admin service account; check the account's group in Onyx's admin users page, then run `sindri onyx up` again");
    }
}
