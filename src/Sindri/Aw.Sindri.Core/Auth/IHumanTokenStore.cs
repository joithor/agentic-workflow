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

    /// <summary>Removes the stored token (a no-op when there is none).</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the error that kept it from being removed.</returns>
    public ValueTask<Result<Unit>> Remove(CancellationToken cancellationToken);
}
