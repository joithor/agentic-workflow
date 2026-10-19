using Aw.Context.Core.Onyx;
using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.OnyxAccount;

/// <summary>
/// Spec §4.1 steps 7 and 8: Sindri's admin key. The owner's Onyx login is asked for once at the terminal, held in memory for the calls below and dropped; the key is made, checked and stored, and only a pointer to it is returned.
/// A recorded key is trusted again only when it still resolves from where it was stored and Onyx still takes it as an admin service account.
/// </summary>
internal sealed class OnyxKeyMinter(IOnyxPrompts prompts, IOnyxProvisioning provisioning, ISecretWriter secrets, ISecretPointers pointers, IRandomHex random)
{
    private const string KeyName = "sindri-onyx";
    private const string KeyService = "onyx";

    /// <summary>What the owner is told whenever a key named sindri-onyx may be left in Onyx with nobody holding it (the allowlist has no delete request).</summary>
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

    internal ValueTask<Result<string>> Mint(int port, CancellationToken cancellationToken)
    {
        _ = (prompts, secrets, random, port, cancellationToken, KeyName, KeyService);
        throw new NotSupportedException("red");
    }
}
