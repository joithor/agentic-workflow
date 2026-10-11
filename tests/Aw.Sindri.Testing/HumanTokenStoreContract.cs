using Aw.Sindri.Core.Auth;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IHumanTokenStore"/> honours: nothing before a mint, a read returns the minted token, a new
/// mint replaces it, and a remove leaves none (also when there was none). Run against the fake and the file adapter.
/// </summary>
public abstract class HumanTokenStoreContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IHumanTokenStore Create();

    /// <summary>No token before the first mint.</summary>
    [Fact]
    public async Task Read_before_any_mint_is_null()
    {
        var read = await Create().Read(Token);

        Expect.True(read.IsSuccess);
        Expect.True(read.Value is null);
    }

    /// <summary>A read returns the minted token.</summary>
    [Fact]
    public async Task Read_returns_the_minted_token()
    {
        var store = Create();
        var minted = (await store.Mint(Token)).Value;

        Expect.Equal(minted, (await store.Read(Token)).Value);
    }

    /// <summary>A second mint replaces the first: only the new token matches.</summary>
    [Fact]
    public async Task A_new_mint_replaces_the_old_token()
    {
        var store = Create();
        var first = (await store.Mint(Token)).Value;
        var second = (await store.Mint(Token)).Value;

        var read = (await store.Read(Token)).Value;
        Expect.Equal(second, read);
        Expect.True(!second.Matches(first.Value));
    }

    /// <summary>A remove leaves no token, and removing again is harmless.</summary>
    [Fact]
    public async Task Remove_leaves_no_token_and_a_second_remove_succeeds()
    {
        var store = Create();
        _ = await store.Mint(Token);

        Expect.True((await store.Remove(Token)).IsSuccess);
        Expect.True((await store.Remove(Token)).IsSuccess);
        Expect.True((await store.Read(Token)).Value is null);
    }
}
