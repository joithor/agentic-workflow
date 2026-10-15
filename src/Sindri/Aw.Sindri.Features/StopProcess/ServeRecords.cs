using Aw.Kernel;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Features.StopProcess;

/// <summary>
/// The files a running serve leaves for its clients, which a stop removes: control.json and the per-start human token.
/// Both go only while they are still this start's (a serve that lost its lease leaves its successor's alone).
/// </summary>
internal sealed class ServeRecords(IEndpointFile endpoints, HeldHumanToken token, IHumanTokenStore tokens)
{
    /// <summary>Removes control.json if it still names <paramref name="pid"/>.</summary>
    /// <param name="pid">This process.</param>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>Success, or the error that kept the record from being removed.</returns>
    public ValueTask<Result<Unit>> RemoveEndpoint(int pid, CancellationToken cancellationToken) => endpoints.Remove(pid, cancellationToken);

    /// <summary>Removes this start's human token file if it still holds this start's token, then forgets the token (a failed removal keeps it held).</summary>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>Success (also when there was no token), or the error that kept the file from being removed.</returns>
    public async ValueTask<Result<Unit>> RemoveToken(CancellationToken cancellationToken)
    {
        var mine = token.Current;
        if (mine is null)
        {
            return Unit.Value;
        }

        var removed = await tokens.Remove(mine, cancellationToken).ConfigureAwait(false);
        if (removed.IsSuccess)
        {
            token.Clear();   // only once the file is gone: a failed removal stays held, for serve's final DropToken to retry
        }

        return removed;
    }
}
