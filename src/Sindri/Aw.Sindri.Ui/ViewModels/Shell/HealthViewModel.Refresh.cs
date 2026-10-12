namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Refresh beside a stale pill (<see cref="HealthViewModel"/>, continued; §4.1 Stale and "sending").</summary>
internal sealed partial class HealthViewModel
{
    private readonly List<Func<Uri, CancellationToken, Task>> _views = [];

    /// <summary>The API's base URL while connected, else null (commands and the sheet read it at the click).</summary>
    /// <returns>The URL, or null.</returns>
    internal Uri? ApiUrl() => _url;

    /// <summary>Adds a view the follower re-reads on connect and after every event (§9.2: re-query, never fold).</summary>
    /// <param name="view">Reads the view from the API's base URL.</param>
    internal void Follow(Func<Uri, CancellationToken, Task> view) => _views.Add(view);

    /// <summary>Re-reads every followed view, in the order they were added.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when every view is shown.</returns>
    internal async Task RefreshViews(Uri url, CancellationToken cancellationToken)
    {
        foreach (var view in _views)
        {
            await view(url, cancellationToken).ConfigureAwait(true);
        }
    }

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
