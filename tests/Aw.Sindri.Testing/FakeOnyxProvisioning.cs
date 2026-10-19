using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>
/// Onyx's setup-only requests, scripted as a small Onyx: it knows the users registered through it (with their current passwords), which of them are admins, and the tokens it made.
/// A login as an email it doesn't know is the owner's and is accepted; a login as a registered user needs that user's current password. Remembers whether each session logged out.
/// </summary>
public sealed class FakeOnyxProvisioning : IOnyxProvisioning
{
    /// <summary>Gets or sets the token a new session makes.</summary>
    public string Token { get; set; } = "onyx_pat_sindri-0123456789";

    /// <summary>Gets or sets an error SignIn returns for any login (the owner's, in a test of a refused login).</summary>
    public Error? SignInError { get; set; }

    /// <summary>Gets or sets an error RegisterUser returns (invite-only, a password Onyx won't take).</summary>
    public Error? RegisterError { get; set; }

    /// <summary>Gets or sets an error ResetPassword returns.</summary>
    public Error? ResetError { get; set; }

    /// <summary>Gets or sets an error MakeAdmin returns (SND-ONYX-007 for a login that isn't an admin).</summary>
    public Error? AdminError { get; set; }

    /// <summary>Gets or sets an error CreateToken returns.</summary>
    public Error? TokenError { get; set; }

    /// <summary>Gets or sets a value indicating whether Onyx takes a made token as its user's admin token.</summary>
    public bool TokenIsAdminAnswer { get; set; } = true;

    /// <summary>Gets the registered users by email, with the password each has now.</summary>
    public IDictionary<string, string> Registered { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the emails that were made admins.</summary>
    public ISet<string> Admins { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Gets the tokens Onyx has made, by the email of the user each belongs to.</summary>
    public IDictionary<string, string> Made { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the number of sign-ins.</summary>
    public int SignIns { get; private set; }

    /// <summary>Gets the number of sessions that were logged out.</summary>
    public int LoggedOut { get; private set; }

    /// <summary>Gets the number of password resets.</summary>
    public int Resets { get; private set; }

    /// <inheritdoc />
    public ValueTask<Result<IOnyxSession>> SignIn(int port, string email, string password, CancellationToken cancellationToken)
    {
        SignIns++;
        if (SignInError is { } error)
        {
            return ValueTask.FromResult<Result<IOnyxSession>>(error);
        }

        if (Registered.TryGetValue(email, out var current) && !string.Equals(current, password, StringComparison.Ordinal))
        {
            return ValueTask.FromResult<Result<IOnyxSession>>(SindriErrors.OnyxKeyFailed.With("Onyx refused that login"));
        }

        return ValueTask.FromResult(Results.Success<IOnyxSession>(new Session(this, email)));
    }

    /// <inheritdoc />
    public ValueTask<Result<bool>> TokenIsAdmin(int port, string token, string email, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<bool>>(Made.TryGetValue(token, out var owner) && string.Equals(owner, email, StringComparison.Ordinal) && Admins.Contains(email) && TokenIsAdminAnswer);

    private sealed class Session(FakeOnyxProvisioning onyx, string email) : IOnyxSession
    {
        public ValueTask<Result<bool>> RegisterUser(string newEmail, string password, CancellationToken cancellationToken)
        {
            if (onyx.RegisterError is { } error)
            {
                return ValueTask.FromResult<Result<bool>>(error);
            }

            var created = onyx.Registered.TryAdd(newEmail, password);
            return ValueTask.FromResult<Result<bool>>(created);
        }

        public ValueTask<Result<string>> ResetPassword(string userEmail, CancellationToken cancellationToken)
        {
            if (onyx.ResetError is { } error)
            {
                return ValueTask.FromResult<Result<string>>(error);
            }

            if (!onyx.Registered.ContainsKey(userEmail))
            {
                return ValueTask.FromResult<Result<string>>(SindriErrors.OnyxKeyFailed.With("Onyx has no such user"));
            }

            var fresh = "reset-" + (++onyx.Resets).ToString(CultureInfo.InvariantCulture) + "-Pw1!";
            onyx.Registered[userEmail] = fresh;
            return ValueTask.FromResult<Result<string>>(fresh);
        }

        public ValueTask<Result<Unit>> MakeAdmin(string userEmail, CancellationToken cancellationToken)
        {
            if (onyx.AdminError is { } error)
            {
                return ValueTask.FromResult<Result<Unit>>(error);
            }

            _ = onyx.Admins.Add(userEmail);
            return ValueTask.FromResult(Results.Ok());
        }

        public ValueTask<Result<string>> CreateToken(string name, CancellationToken cancellationToken)
        {
            if (onyx.TokenError is { } error)
            {
                return ValueTask.FromResult<Result<string>>(error);
            }

            onyx.Made[onyx.Token] = email;
            return ValueTask.FromResult<Result<string>>(onyx.Token);
        }

        public ValueTask DisposeAsync()
        {
            onyx.LoggedOut++;
            return ValueTask.CompletedTask;
        }
    }
}
