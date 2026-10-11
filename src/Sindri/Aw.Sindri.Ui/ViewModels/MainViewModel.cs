using System.Globalization;
using System.Threading.Channels;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>
/// The main window: the control plane's status, kept live from its event stream. It finds the control plane through
/// its endpoint record, checks the record against /sindri/ping (a stale record reads as not running), shows
/// /sindri/status, and refreshes the status after each event on /sindri/events. When the stream ends it looks again.
/// Every await resumes on the caller's context (the UI thread in the app), so property changes reach the bindings there.
/// </summary>
/// <param name="records">Where the control plane recorded its endpoint.</param>
/// <param name="api">Its API.</param>
/// <param name="clock">The clock for retry waits.</param>
internal sealed class MainViewModel(IEndpointRecordSource records, ISindriApi api, TimeProvider clock) : ObservableObject
{
    /// <summary>How long to wait before looking for the control plane again.</summary>
    internal static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(2);

    private long _cursor;
    private byte[] _record = [];

    /// <summary>The state in one word: Connecting, Running or Not running.</summary>
    public string Headline { get; private set => Set(ref field, value); } = "Connecting";

    /// <summary>Why it isn't running (when it isn't).</summary>
    public string Reason { get; private set => Set(ref field, value); } = "looking for Sindri's control plane";

    /// <summary>The command that fixes "not running".</summary>
    public string Fix { get; } = StatusText.StartFix;

    /// <summary>True while a control plane answers.</summary>
    public bool IsRunning { get; private set => Set(ref field, value); }

    /// <summary>True once it's known that none does.</summary>
    public bool IsNotRunning { get; private set => Set(ref field, value); }

    /// <summary>Running: the lease line.</summary>
    public string Lease { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the API listens.</summary>
    public string Api { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The profile's state in words.</summary>
    public string ProfileState { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The profile's hash and file.</summary>
    public string ProfileDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The command that fixes the profile's state, or empty.</summary>
    public string ProfileFix
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasProfileFix));
        }
    } = string.Empty;

    /// <summary>True when the profile needs a command.</summary>
    public bool HasProfileFix => ProfileFix.Length > 0;

    /// <summary>The scheduled services.</summary>
    public IReadOnlyList<ServiceRow> Services
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasServices));
        }
    } = [];

    /// <summary>True when any service is scheduled.</summary>
    public bool HasServices => Services.Count > 0;

    /// <summary>The newest event seen on the stream.</summary>
    public string LastEvent { get; private set => Set(ref field, value); } = "none yet";

    /// <summary>Follows the control plane until cancelled.</summary>
    /// <param name="cancellationToken">Stops following.</param>
    /// <returns>A task that ends when cancelled.</returns>
    internal async Task Run(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var url = await Connect(cancellationToken).ConfigureAwait(true);
            if (url is not null)
            {
                await Follow(url, cancellationToken).ConfigureAwait(true);
                continue;
            }

            try
            {
                await Task.Delay(RetryEvery, clock, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<Uri?> Connect(CancellationToken cancellationToken)
    {
        var bytes = await records.Read(cancellationToken).ConfigureAwait(true);
        if (bytes is null)
        {
            return NotRunning($"no endpoint record at {records.Location}: Sindri hasn't started, or it stopped");
        }

        var record = AwJson.Deserialize<ControlRecord>(bytes, SindriApiClient.Json);
        if (record.IsError)
        {
            return NotRunning($"the endpoint record at {records.Location} can't be read");
        }

        var ping = await api.Ping(record.Value.Url, cancellationToken).ConfigureAwait(true);
        if (ping.IsError)
        {
            return NotRunning(ping.Error.Message);
        }

        if (ping.Value.Pid != record.Value.Pid || ping.Value.Epoch != record.Value.Epoch)
        {
            return NotRunning(string.Create(CultureInfo.InvariantCulture,
                $"the endpoint record is stale: it names pid {record.Value.Pid} at epoch {record.Value.Epoch}, but pid {ping.Value.Pid} at epoch {ping.Value.Epoch} answers"));
        }

        _record = bytes;
        return await Refresh(record.Value.Url, cancellationToken).ConfigureAwait(true) ? record.Value.Url : null;
    }

    // Follows the stream until it ends, a status refresh fails, or the endpoint record changes. A stopping control plane
    // can hold its stream open while it shuts down, so the stream ending is not the only sign that it's gone.
    private async Task Follow(Uri url, CancellationToken cancellationToken)
    {
        var dirty = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var refresher = RefreshOnSignal(url, dirty.Reader, stop);
        var watcher = WatchRecord(stop);
        try
        {
            await foreach (var e in api.Events(url, _cursor, stop.Token).ConfigureAwait(true))
            {
                _cursor = e.Sequence;
                LastEvent = StatusText.Event(e);
                _ = dirty.Writer.TryWrite(item: true);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // A failed refresh or a changed record ended the stream; Run looks for the control plane again.
        }

        await stop.CancelAsync().ConfigureAwait(true);
        await Task.WhenAll(refresher, watcher).ConfigureAwait(true);
    }

    // One status refresh per burst of events: the signal holds at most one pending refresh. A failed refresh ends the follow.
    private async Task RefreshOnSignal(Uri url, ChannelReader<bool> dirty, CancellationTokenSource stop)
    {
        try
        {
            while (await dirty.WaitToReadAsync(stop.Token).ConfigureAwait(true))
            {
                _ = dirty.TryRead(item: out _);
                if (!await Refresh(url, stop.Token).ConfigureAwait(true))
                {
                    await stop.CancelAsync().ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The follow ended.
        }
    }

    // Ends the follow when the endpoint record no longer is the one this follow connected with (stopped, or restarted).
    private async Task WatchRecord(CancellationTokenSource stop)
    {
        try
        {
            while (true)
            {
                await Task.Delay(RetryEvery, clock, stop.Token).ConfigureAwait(true);
                var now = await records.Read(stop.Token).ConfigureAwait(true);
                if (now is null || !now.AsSpan().SequenceEqual(_record))
                {
                    await stop.CancelAsync().ConfigureAwait(true);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The follow ended.
        }
    }

    private async Task<bool> Refresh(Uri url, CancellationToken cancellationToken)
    {
        var status = await api.Status(url, cancellationToken).ConfigureAwait(true);
        if (status.IsError)
        {
            _ = NotRunning(status.Error.Message);
            return false;
        }

        Show(url, status.Value);
        return true;
    }

    private void Show(Uri url, StatusView status)
    {
        Api = url.Authority;
        Lease = StatusText.Lease(status);
        ProfileState = StatusText.ProfileState(status.Profile);
        ProfileDetail = StatusText.ProfileDetail(status.Profile);
        ProfileFix = StatusText.ProfileFix(status.Profile);
        Services = [.. status.Services.Select(StatusText.Service)];
        Headline = status.Running ? "Running" : "Not running";
        Reason = status.Running ? string.Empty : "the control plane answers but holds no lease";
        IsRunning = status.Running;
        IsNotRunning = !status.Running;
    }

    private Uri? NotRunning(string reason)
    {
        Headline = "Not running";
        Reason = reason;
        IsRunning = false;
        IsNotRunning = true;
        return null;
    }
}
