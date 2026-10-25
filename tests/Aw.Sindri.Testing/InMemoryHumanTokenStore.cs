using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Auth;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory human token store: the one fake, kept honest by HumanTokenStoreContract. Tokens are 'token-1', 'token-2', …</summary>
public sealed class InMemoryHumanTokenStore : IHumanTokenStore
{
    private HumanToken? _token;
    private int _minted;

    /// <summary>A store that already holds a token (what a running control plane leaves): the CLI reading it calls as a human.</summary>
    /// <returns>The store, holding 'token-1'.</returns>
    public static InMemoryHumanTokenStore WithToken() => new() { _token = new HumanToken("token-1"), _minted = 1 };

    /// <inheritdoc/>
    public ValueTask<Result<HumanToken>> Mint(CancellationToken cancellationToken)
    {
        var token = new HumanToken(string.Create(CultureInfo.InvariantCulture, $"token-{++_minted}"));
        _token = token;
        return ValueTask.FromResult<Result<HumanToken>>(token);
    }

    /// <inheritdoc/>
    public ValueTask<Result<HumanToken?>> Read(CancellationToken cancellationToken) => ValueTask.FromResult<Result<HumanToken?>>(_token);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Remove(HumanToken mine, CancellationToken cancellationToken)
    {
        if (_token is not null && _token.Matches(mine.Value))
        {
            _token = null;
        }

        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}
