namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>
/// POST /sindri/onyx/account's body (setup-once spec §5.6 ConnectOnyx, desk): the login the person made in the browser. The password is used for this one call: its text never
/// leaves this type through <see cref="ToString"/>, a log or an error, and the caller keeps no copy.
/// </summary>
/// <param name="AdminEmail">The Onyx account's email.</param>
/// <param name="AdminPassword">The Onyx account's password.</param>
internal sealed record OnyxLogin(string AdminEmail, string AdminPassword)
{
    /// <summary>The type's name, never its values: a record would print the password.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => nameof(OnyxLogin);
}
