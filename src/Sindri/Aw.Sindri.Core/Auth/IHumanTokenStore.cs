using Aw.Kernel;

namespace Aw.Sindri.Core.Auth;

/// <summary>Where the per-start human token lives (API spec §6: '&lt;home&gt;/auth/human.token', 0600 in a 0700 folder).</summary>
public interface IHumanTokenStore
{
    /// <summary>Mints a new random token and stores it, replacing any earlier one.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new token, or the error that kept it from being stored.</returns>
    public ValueTask<Result<HumanToken>> Mint(CancellationToken cancellationToken);

    /// <summary>Reads the stored token.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The token, null when none is stored, or the error that kept it from being read.</returns>
    public ValueTask<Result<HumanToken?>> Read(CancellationToken cancellationToken);

    /// <summary>
    /// Removes the stored token if it is still <paramref name="mine"/> (a no-op when there is none or another start's):
    /// a serve that stops after losing its lease must not delete its successor's token.
    /// </summary>
    /// <param name="mine">The token this start minted.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the error that kept it from being read or removed.</returns>
    public ValueTask<Result<Unit>> Remove(HumanToken mine, CancellationToken cancellationToken);
}
