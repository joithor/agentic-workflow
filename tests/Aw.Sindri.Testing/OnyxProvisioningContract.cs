using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IOnyxProvisioning"/> honours: a login makes a key Onyx then treats as an admin, a stranger's key is not, and disposing the session logs out. Run against the fake and HttpOnyxProvisioning over a scripted Onyx.</summary>
public abstract class OnyxProvisioningContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A provisioning over an Onyx that accepts the login.</summary>
    /// <returns>The subject and a count of its logouts.</returns>
    protected abstract (IOnyxProvisioning Provisioning, Func<int> Logouts) Create();

    /// <summary>A key made in a session is an admin key.</summary>
    [Fact]
    public async Task A_key_made_in_a_session_is_an_admin_key_and_a_stranger_is_not()
    {
        var (provisioning, _) = Create();
        var session = (await provisioning.SignIn(3000, "owner@example.com", "pw", Token)).Value;
        string key;
        await using (session)
        {
            var group = (await session.AdminGroup(Token)).Value;
            key = (await session.CreateKey("sindri-onyx", group, Token)).Value;
        }

        Expect.True(key.Length > 0);
        Expect.True((await provisioning.KeyIsAdmin(3000, key, Token)).Value);
        Expect.True(!(await provisioning.KeyIsAdmin(3000, "stranger-key", Token)).Value);
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
