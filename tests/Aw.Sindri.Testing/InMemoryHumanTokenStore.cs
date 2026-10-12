using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Auth;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory human token store: the one fake, kept honest by HumanTokenStoreContract. Tokens are 'token-1', 'token-2', …</summary>
public sealed class InMemoryHumanTokenStore : IHumanTokenStore
{
    private HumanToken? _token;
    private int _minted;

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
