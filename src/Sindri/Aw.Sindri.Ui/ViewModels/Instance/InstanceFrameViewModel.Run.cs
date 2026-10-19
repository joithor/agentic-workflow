using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The instance frame's agent run (<see cref="InstanceFrameViewModel"/>, continued): reads the launch, the item and the events behind an agent Step.</summary>
internal sealed partial class InstanceFrameViewModel
{
    private string? _agentAddress;

    // An agent Step shows a run line, its launch's state and Attach (ready from the item), and what the agent's Steps came to.
    // The events are read while this instance has had an agent Step in this frame, so a failure that moves it to a desk twin or a
    // park still reads after the run is gone. Nothing is read, and nothing is drawn, for any other instance.
    private async Task ReadRun(InstanceDetail detail, int generation, CancellationToken cancellationToken)
    {
        if (Expanded is not { } expanded || _url is not { } url)
        {
            return;
        }

        var run = detail.Current.Status.Run;
        if (expanded.HasAttach || run is not null)
        {
            _agentAddress = detail.Address;
        }

        if (!string.Equals(_agentAddress, detail.Address, StringComparison.Ordinal))
        {
            return;
        }

        if (detail.ItemId is { } itemId)
        {
            expanded.Attacher ??= new AgentAttacher(api, () => _url, itemId);
        }

        var (item, unreadable) = expanded.HasAttach ? await ReadItem(url, detail.ItemId, cancellationToken).ConfigureAwait(true) : (null, false);
        var launch = await ReadLaunch(url, run, cancellationToken).ConfigureAwait(true);
        var events = await api.InstanceEvents(url, detail.Id, cancellationToken).ConfigureAwait(true);
        if (generation == _generation)
        {
            expanded.ShowRun(new AgentRunRead(launch, item, events.IsSuccess ? events.Value.Events : [], unreadable));
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
}
