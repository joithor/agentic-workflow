namespace Aw.Sindri.Core.Onyx;

/// <summary>The Onyx accounts `sindri onyx up` knows by address.</summary>
public static class OnyxAccounts
{
    /// <summary>
    /// The dedicated Onyx user Sindri makes for itself (an admin, so the search route works, on its own account rather than a person's). Context's `OnyxSettings.DedicatedAccountEmail` is the same address, which doctor
    /// compares a token's account with (a test holds the two together; the Sindri Infrastructure project can't reference Context).
    /// An example.com address on purpose: RFC 2606 reserves the name and its null MX (RFC 7505) means no mail to it is delivered, so nobody can own the mailbox and Onyx's forgot-password or invite mail reaches no one.
    /// Onyx accepts it: its pinned email-validator 2.2.0 leaves example.com out of its special-use list (`email_validator/__init__.py:66`), where `.invalid`, `.test`, `.local` and `.localhost` are rejected.
    /// </summary>
    public const string DedicatedEmail = "sindri@example.com";

    /// <summary>The dedicated user's address in an earlier build (a real domain Sindri doesn't control). `sindri onyx up` deactivates a user with this email if Onyx has one, and never makes one.</summary>
    public const string LegacyEmail = "sindri@sindri.app";
}
