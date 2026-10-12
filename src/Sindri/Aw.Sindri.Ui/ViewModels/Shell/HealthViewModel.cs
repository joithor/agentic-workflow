using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The control plane's health (S1b's status card, the Health page in U0) and the connection behind the shell's pre-API
/// state (UI spec §4.1). It finds the control plane through its endpoint record, checks the record against /sindri/ping
/// (a stale record reads as not running), shows /sindri/status, and refreshes the status after each event on
/// /sindri/events. When the stream ends it looks again. Every await resumes on the caller's context (the UI thread in the
/// app), so property changes reach the bindings there; the freshness ticker posts to that context too.
/// </summary>
/// <param name="records">Where the control plane recorded its endpoint.</param>
/// <param name="api">Its API.</param>
/// <param name="clock">The clock for retry waits and freshness.</param>
internal sealed partial class HealthViewModel(IEndpointRecordSource records, ISindriApi api, TimeProvider clock) : ObservableObject
{
    /// <summary>How long to wait before looking for the control plane again.</summary>
    internal static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(2);

    /// <summary>How long without an event, a status or an answered ping before the views are stale (§4.1, API §2.4).</summary>
    internal static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);

    /// <summary>How long a refresh is in flight before "Sending…" shows (§4.1 "sending").</summary>
    internal static readonly TimeSpan SendingAfter = TimeSpan.FromMilliseconds(300);

    /// <summary>How often the follow pings the control plane it follows, well inside <see cref="StaleAfter"/>.</summary>
    internal static readonly TimeSpan PingEvery = TimeSpan.FromSeconds(10);

    // How often "updated Ns ago" is re-worded.
    private static readonly TimeSpan _tick = TimeSpan.FromSeconds(1);

    // The ticker runs on the clock's thread; in tests (no UI context) it runs inline, so state changes take this lock.
    private readonly Lock _gate = new();
    private DateTimeOffset _heard;
    private Uri? _url;
    private int _failedRefreshes;
    private int _refreshes;
    private SynchronizationContext? _ui;

    /// <summary>The app's copy, for the view.</summary>
    public ShellText Text { get; } = ShellText.Instance;

    /// <summary>The pre-API state (§4.1).</summary>
    public ShellState State { get; private set => Set(ref field, value); } = ShellState.Connecting;

    /// <summary>The health pill's parts.</summary>
    public HealthPill Pill { get; } = new();

    /// <summary>The status as labelled values: Home's strip and the Health page's rows.</summary>
    public HealthDetail Detail { get; } = new();

    /// <summary>Home's health strip (§4.2, §4.8): the status's fields in the health line's order, or the state in words.</summary>
    public string HealthLine { get; private set => Set(ref field, value); } = Labels.Connecting;

    /// <summary>The state in one word: Connecting…, Running or Not running.</summary>
    public string Headline { get; private set => Set(ref field, value); } = Labels.Connecting;

    /// <summary>Why it isn't running, the two versions, or the unreadable answer's code and message; empty otherwise.</summary>
    public string Reason { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The command that fixes "not running".</summary>
    public string Fix { get; } = Labels.StartFix;

    /// <summary>True while a control plane answers.</summary>
    public bool IsRunning { get; private set => Set(ref field, value); }

    /// <summary>True once it's known that none does.</summary>
    public bool IsNotRunning { get; private set => Set(ref field, value); }

    /// <summary>Refresh takes a click: false while one is in flight (§4.1 "sending": disabled, its label kept; a second click does nothing).</summary>
    public bool CanRefresh { get; private set => Set(ref field, value); } = true;

    /// <summary>A refresh has been in flight for 300 ms: "Sending…" is announced politely (§4.1).</summary>
    public bool IsSending
    {
        get;
        private set
        {
            Set(ref field, value);
            SendingAnnouncement = value ? Labels.Sending : string.Empty;
        }
    }

    /// <summary>The newest event seen on the stream.</summary>
    public string LastEvent { get; private set => Set(ref field, value); } = "none yet";

    /// <summary>Follows the control plane until cancelled, re-wording the freshness every second.</summary>
    /// <param name="cancellationToken">Stops following.</param>
    /// <returns>A task that ends when cancelled.</returns>
    internal async Task Run(CancellationToken cancellationToken)
    {
        _ui = SynchronizationContext.Current;
        var ticker = clock.CreateTimer(_ => OnUi(Age), state: null, _tick, _tick);
        await using (ticker.ConfigureAwait(true))
        {
            await new ControlPlaneFollower(records, api, clock, this).Run(cancellationToken).ConfigureAwait(true);
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

    /// <summary>An event on the stream: the newest event, and the views are fresh.</summary>
    /// <param name="e">The event.</param>
    internal void Event(EventView e)
    {
        LastEvent = StatusText.Event(e);
        Heard();
    }

    /// <summary>The followed control plane answered a ping: it is there, so the views are fresh (§4.1).</summary>
    internal void Answered() => Heard();

    /// <summary>A status: the card's words, or a version mismatch.</summary>
    /// <param name="url">Where the API answered.</param>
    /// <param name="status">The status.</param>
    internal void Show(Uri url, StatusView status)
    {
        if (status.ApiVersion is { } version && version != SindriApiClient.AppApiVersion)
        {
            Mismatch(version);
            return;
        }

        if (!status.Running)
        {
            _ = NotRunning(Labels.NoLease);
        }

        _url = url;
        Detail.Show(url, status);
        Api = url.Authority;
        Lease = StatusText.Lease(status);
        ProfileState = StatusText.ProfileState(status.Profile);
        ProfileDetail = StatusText.ProfileDetail(status.Profile);
        ProfileFix = StatusText.ProfileFix(status.Profile);
        Services = [.. status.Services.Select(StatusText.Service)];
        if (status.Running)
        {
            lock (_gate)
            {
                Headline = Labels.Running;
                HealthLine = StatusText.HealthLine(status);
                Reason = string.Empty;
                IsRunning = true;
                IsNotRunning = false;
                State = ShellState.Normal;
            }

            Heard();
        }
    }

    /// <summary>A failed call: SND-UI-003 is an answer this build can't read (Unreadable, the last data kept); anything else is Not running.</summary>
    /// <param name="error">The call's error.</param>
    /// <returns>Null: no API to follow.</returns>
    internal Uri? Failed(Error error) =>
        string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal) ? Unreadable(error) : NotRunning(error.Message);

    /// <summary>Not running, and why.</summary>
    /// <param name="reason">The reason in words.</param>
    /// <returns>Null: no API to follow.</returns>
    internal Uri? NotRunning(string reason)
    {
        lock (_gate)
        {
            Headline = Labels.NotRunning;
            HealthLine = Labels.NotRunning;
            Reason = reason;
            IsRunning = false;
            IsNotRunning = true;
            State = ShellState.NotRunning;
            Word();
        }

        return null;
    }

    private Uri? Unreadable(Error error)
    {
        lock (_gate)
        {
            Reason = $"{error.Code}: {error.Message}";
            State = ShellState.Unreadable;
            Word();
        }

        return null;
    }

    private void Mismatch(int version)
    {
        lock (_gate)
        {
            Reason = Labels.Versions(version, SindriApiClient.AppApiVersion);
            Detail.Mismatch(version);
            HealthLine = Labels.VersionMismatch;
            State = ShellState.VersionMismatch;
            Word();
        }
    }

    // An event, a status or an answered ping: the views are fresh again.
    private void Heard()
    {
        lock (_gate)
        {
            _heard = clock.GetUtcNow();
            _failedRefreshes = 0;
            if (State is ShellState.Normal or ShellState.Stale)
            {
                State = ShellState.Normal;
                Word();
            }
        }
    }

    // The ticker: "updated Ns ago", and after StaleAfter without an event, a status or an answered ping the word stale.
    private void Age()
    {
        lock (_gate)
        {
            if (State is not (ShellState.Normal or ShellState.Stale))
            {
                return;
            }

            if (clock.GetUtcNow() - _heard >= StaleAfter)
            {
                State = ShellState.Stale;
            }

            Word();
        }
    }

    // The pill for the state now (under the gate): its age counts from the last event, status or answered ping.
    private void Word() => Pill.Show(State, clock.GetUtcNow() - _heard, _failedRefreshes >= NotRespondingAfter);

    // §4.1 Stale: "After 3 failed refreshes: Sindri not responding: run sindri doctor".
    private const int NotRespondingAfter = 3;

    private void OnUi(Action action)
    {
        if (_ui is { } ui)
        {
            ui.Post(_ => action(), state: null);
        }
        else
        {
            action();
        }
    }
}
