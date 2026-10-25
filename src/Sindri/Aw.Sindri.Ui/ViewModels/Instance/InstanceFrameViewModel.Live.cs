using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame's answer to an instance change (<see cref="InstanceFrameViewModel"/>, continued).</summary>
internal sealed partial class InstanceFrameViewModel
{
    private string _state = string.Empty;   // the state the view served last, to compare with a frame's

    /// <summary>Re-reads the instance shown when a change names it (or for any change when <paramref name="change"/> is null).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="change">The frame, or null to re-read whatever is shown.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>False when the view still isn't in the frame's state; true when it is, or when the change isn't about this frame.</returns>
    internal async Task<bool> Catch(Uri url, InstanceChange? change, CancellationToken cancellationToken)
    {
        if (Address.Length == 0 || (change is not null && !string.Equals(change.Instance, Address, StringComparison.Ordinal)))
        {
            return true;   // nothing shown, or another instance: nothing to read or wait for
        }

        _url = url;
        await Read(cancellationToken).ConfigureAwait(true);
        return change is null || (Phase == FramePhase.Shown && string.Equals(_state, change.State, StringComparison.Ordinal));
    }
}
