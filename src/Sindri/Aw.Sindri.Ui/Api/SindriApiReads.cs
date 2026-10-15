using Aw.Kernel;

namespace Aw.Sindri.Ui.Api;

/// <summary>The reads of <see cref="ISindriApi"/> the app makes the same way wherever it makes them.</summary>
internal static class SindriApiReads
{
    /// <summary>
    /// The status, read once more at once when the views were behind the sequence of the last command. Read-your-writes is
    /// best effort (API §2.4): the first read used the sequence up, so the second shows what Sindri says (stalled,
    /// rebuilding). The follower and a manual Refresh both read the status this way.
    /// </summary>
    /// <param name="api">The API.</param>
    /// <param name="url">The base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The status, or the error of the last read.</returns>
    internal static async Task<Result<StatusView>> ReadStatus(this ISindriApi api, Uri url, CancellationToken cancellationToken)
    {
        var status = await api.Status(url, cancellationToken).ConfigureAwait(false);
        return status.IsError && IsViewsBehind(status.Error) ? await api.Status(url, cancellationToken).ConfigureAwait(false) : status;
    }

    /// <summary>True for SND-API-011: the views have not reached the sequence the read asked for.</summary>
    /// <param name="error">The error.</param>
    /// <returns>True when the views are behind.</returns>
    internal static bool IsViewsBehind(Error error) => string.Equals(error.Code, SindriApiClient.ViewsBehind, StringComparison.Ordinal);
}
