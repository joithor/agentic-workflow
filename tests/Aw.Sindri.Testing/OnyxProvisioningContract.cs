using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IOnyxProvisioning"/> honours: the owner's session registers a user once (a second try says it exists), resets its password and makes it an admin; that user's own login makes a token
/// that Onyx then takes as the user's admin token, and a stranger's is not; disposing a session logs out. Run against the fake and HttpOnyxProvisioning over a scripted Onyx.
/// </summary>
public abstract class OnyxProvisioningContract
{
    private const string Email = "sindri@sindri.app";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A provisioning over an Onyx that accepts the owner's login.</summary>
    /// <returns>The subject and a count of its logouts.</returns>
    protected abstract (IOnyxProvisioning Provisioning, Func<int> Logouts) Create();

    /// <summary>The whole dedicated-user flow.</summary>
    [Fact]
    public async Task A_user_made_by_the_owner_gets_an_admin_token_of_its_own_and_a_stranger_does_not()
    {
        var (provisioning, _) = Create();
        string password;
        var owner = (await provisioning.SignIn(3000, "owner@example.com", "pw", Token)).Value;
        await using (owner)
        {
            Expect.True((await owner.RegisterUser(Email, "first-pw", Token)).Value);
            Expect.True(!(await owner.RegisterUser(Email, "other-pw", Token)).Value);
            password = (await owner.ResetPassword(Email, Token)).Value;
            Expect.True((await owner.MakeAdmin(Email, Token)).IsSuccess);
        }

        string token;
        var dedicated = (await provisioning.SignIn(3000, Email, password, Token)).Value;
        await using (dedicated)
        {
            token = (await dedicated.CreateToken("sindri-onyx", Token)).Value;
        }

        Expect.True(token.Length > 0);
        Expect.True((await provisioning.TokenIsAdmin(3000, token, Email, Token)).Value);
        Expect.True(!(await provisioning.TokenIsAdmin(3000, token, "someone@example.com", Token)).Value);
        Expect.True(!(await provisioning.TokenIsAdmin(3000, "stranger-token", Email, Token)).Value);
    }

    /// <summary>Disposing a session logs it out, once.</summary>
    [Fact]
    public async Task Disposing_the_session_logs_out_once()
    {
        var (provisioning, logouts) = Create();
        var session = (await provisioning.SignIn(3000, "owner@example.com", "pw", Token)).Value;

        await session.DisposeAsync();

        Expect.Equal(1, logouts());
    }
}
