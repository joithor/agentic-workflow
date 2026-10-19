using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>Onyx's setup-only requests, scripted. Remembers the logins it was given (never shown by anything) and whether each session logged out.</summary>
public sealed class FakeOnyxProvisioning : IOnyxProvisioning
{
    /// <summary>Gets or sets the key a new session makes.</summary>
    public string Key { get; set; } = "sindri-key-0123456789";

    /// <summary>Gets or sets an error SignIn returns.</summary>
    public Error? SignInError { get; set; }

    /// <summary>Gets or sets an error AdminGroup returns (SND-ONYX-007 for a non-admin login).</summary>
    public Error? GroupError { get; set; }

    /// <summary>Gets or sets an error CreateKey returns instead of a key (a key whose service account isn't in the Admin group, say).</summary>
    public Error? CreateKeyError { get; set; }

    /// <summary>Gets the group ids each CreateKey was asked to put the key's service account in.</summary>
    public IList<int> KeyGroups { get; } = [];

    /// <summary>Gets or sets a value indicating whether Onyx treats the made key as an admin service account.</summary>
    public bool KeyIsAdminAnswer { get; set; } = true;

    /// <summary>Gets the keys Onyx has made, so KeyIsAdmin can tell a made key from a stranger.</summary>
    public ISet<string> Made { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Gets the number of sign-ins.</summary>
    public int SignIns { get; private set; }

    /// <summary>Gets the number of sessions that were logged out.</summary>
    public int LoggedOut { get; private set; }

    /// <inheritdoc />
    public ValueTask<Result<IOnyxSession>> SignIn(int port, string email, string password, CancellationToken cancellationToken)
    {
        SignIns++;
        return ValueTask.FromResult(SignInError is { } error ? error : Results.Success<IOnyxSession>(new Session(this)));
    }

    /// <inheritdoc />
    public ValueTask<Result<bool>> KeyIsAdmin(int port, string key, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<bool>>(Made.Contains(key) && KeyIsAdminAnswer);

    private sealed class Session(FakeOnyxProvisioning owner) : IOnyxSession
    {
        public ValueTask<Result<int>> AdminGroup(CancellationToken cancellationToken)
            => ValueTask.FromResult(owner.GroupError is { } error ? error : Results.Success(1));

        public ValueTask<Result<string>> CreateKey(string name, int groupId, CancellationToken cancellationToken)
        {
            owner.KeyGroups.Add(groupId);
            if (owner.CreateKeyError is { } error)
            {
                return ValueTask.FromResult<Result<string>>(error);
            }

            _ = owner.Made.Add(owner.Key);
            return ValueTask.FromResult<Result<string>>(owner.Key);
        }

        public ValueTask DisposeAsync()
        {
            owner.LoggedOut++;
            return ValueTask.CompletedTask;
        }
    }
}
