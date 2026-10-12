namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Refresh beside a stale pill (<see cref="HealthViewModel"/>, continued; §4.1 Stale and "sending").</summary>
internal sealed partial class HealthViewModel
{
    /// <summary>
    /// Refresh (§4.1 Stale): reads the status again, a query. While it is in flight Refresh takes no click (a second one
    /// does nothing), and after <see cref="SendingAfter"/> "Sending…" shows; both clear when the answer comes.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Refresh(CancellationToken cancellationToken)
    {
        if (_url is not { } url || !CanRefresh)
        {
            return;
        }

        CanRefresh = false;
        var attempt = ++_refreshes;
        var sending = clock.CreateTimer(_ => OnUi(() => IsSending = !CanRefresh && attempt == _refreshes), state: null, SendingAfter, Timeout.InfiniteTimeSpan);
        try
        {
            await using (sending.ConfigureAwait(true))
            {
                await Answer(url, cancellationToken).ConfigureAwait(true);
            }
        }
        finally
        {
            IsSending = false;
            CanRefresh = true;
        }
    }

    private async Task Answer(Uri url, CancellationToken cancellationToken)
    {
        var status = await api.Status(url, cancellationToken).ConfigureAwait(true);
        if (status.IsSuccess)
        {
            Show(url, status.Value);
            return;
        }

        lock (_gate)
        {
            _failedRefreshes++;
            Word();
        }
    }
}
