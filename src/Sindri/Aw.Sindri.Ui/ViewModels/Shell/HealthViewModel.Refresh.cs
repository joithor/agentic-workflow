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

    // Bumped each time the follow decides a state (a status shown, Not running, Unreadable, a mismatch), so a Refresh
    // whose answer comes after one knows it is late; and the lease the follow checked with ping (pid, epoch).
    private int _decisions;
    private (int Pid, long Epoch) _followed;

    /// <summary>The follow checked a control plane's lease with ping: a Refresh's answer counts only from that process.</summary>
    /// <param name="pid">Its process id.</param>
    /// <param name="epoch">Its lease epoch.</param>
    internal void Follows(int pid, long epoch)
    {
        lock (_gate)
        {
            _followed = (pid, epoch);
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

    // The answer counts only if the follow decided nothing while it was in flight, the views are still Normal or Stale,
    // and it came from the process and epoch the follow checked with ping; otherwise it is dropped (review run 3,
    // julik-frontend-races-reviewer-1), so a late answer never shows Running over Not running or Unreadable.
    private async Task Answer(Uri url, CancellationToken cancellationToken)
    {
        var decided = Volatile.Read(ref _decisions);
        var status = await api.Status(url, cancellationToken).ConfigureAwait(true);
        lock (_gate)
        {
            if (decided != _decisions || State is not (ShellState.Normal or ShellState.Stale))
            {
                return;
            }

            if (status.IsError)
            {
                _failedRefreshes++;
                Word();
                return;
            }

            if (status.Value.Pid != _followed.Pid || status.Value.Epoch != _followed.Epoch)
            {
                return;
            }
        }

        Show(url, status.Value);
    }
}
