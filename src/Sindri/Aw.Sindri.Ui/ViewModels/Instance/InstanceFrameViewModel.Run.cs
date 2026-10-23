using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The instance frame's agent run (<see cref="InstanceFrameViewModel"/>, continued): reads the launch, the item and the events behind an agent Step.</summary>
internal sealed partial class InstanceFrameViewModel
{
    // What the events were last read for: the instance and what it served then. A stream event for the instance (step.*, launch.*,
    // session.ended) moves its version, state or run, so the events are read once when the frame opens and again only when one moved.
    private (string Address, long Version, string State, string? Launch)? _eventsFor;
    private IReadOnlyList<InstanceEventRow> _eventRows = [];

    /// <summary>Raised when the agent Step's session link is followed; the shell opens the Agents page's session sheet.</summary>
    internal event EventHandler<FrameSessionEventArgs>? SessionRequested;

    // An agent Step shows a run line, its launch's state and Attach (ready from the item). What the agent's Steps came to is read for
    // every instance, so a frame opened on one that already failed or completed still shows it; it is nothing for an instance that never ran one.
    private async Task ReadRun(InstanceDetail detail, int generation, CancellationToken cancellationToken)
    {
        if (Expanded is not { } expanded || _url is not { } url)
        {
            return;
        }

        var run = detail.Current.Status.Run;
        if (detail.ItemId is { } itemId)
        {
            expanded.Attacher ??= new AgentAttacher(api, () => _url, itemId);
        }

        var (item, unreadable) = expanded.HasAttach ? await ReadItem(url, detail.ItemId, cancellationToken).ConfigureAwait(true) : (null, false);
        var launch = await ReadLaunch(url, run, cancellationToken).ConfigureAwait(true);
        var sessionRef = await ReadSessionRef(url, run, cancellationToken).ConfigureAwait(true);
        var header = detail.Current.Status.Header;
        var stamp = (detail.Address, header.Version, header.State, run?.Launch);
        if (_eventsFor != stamp)
        {
            var events = await api.InstanceEvents(url, detail.Id, cancellationToken).ConfigureAwait(true);
            if (generation == _generation && events.IsSuccess)
            {
                (_eventsFor, _eventRows) = (stamp, events.Value.Events);
            }
        }

        if (generation == _generation)
        {
            expanded.ShowRun(new AgentRunRead(launch, item, _eventRows, unreadable, sessionRef));
        }
    }

    private async Task<(ItemView? Item, bool Unreadable)> ReadItem(Uri url, string? itemId, CancellationToken cancellationToken)
    {
        if (itemId is null)
        {
            return (null, true);
        }

        var read = await api.Item(url, itemId, cancellationToken).ConfigureAwait(true);
        return read.IsSuccess ? (read.Value, false) : (null, true);
    }

    private async Task<LaunchView?> ReadLaunch(Uri url, StatusRun? run, CancellationToken cancellationToken)
    {
        if (run is null || !string.Equals(run.Runtime, "agent", StringComparison.Ordinal))
        {
            return null;
        }

        var read = await api.Launch(url, run.Launch, cancellationToken).ConfigureAwait(true);
        return read.IsSuccess ? read.Value : null;
    }

    // The session behind the run is known when the collector lists a row whose launch id is the run's; a list that can't be read is no link.
    private async Task<string?> ReadSessionRef(Uri url, StatusRun? run, CancellationToken cancellationToken)
    {
        if (run?.Session is { } served)
        {
            return served.Ref;   // the engine names the session: no lookup
        }

        if (run is null || !string.Equals(run.Runtime, "agent", StringComparison.Ordinal))
        {
            return null;
        }

        var read = await api.Sessions(url, new SessionQuery(InWorkflow: true), cancellationToken).ConfigureAwait(true);
        return read.IsSuccess ? read.Value.Sessions.FirstOrDefault(s => string.Equals(s.LaunchId, run.Launch, StringComparison.Ordinal))?.Ref : null;
    }
}
