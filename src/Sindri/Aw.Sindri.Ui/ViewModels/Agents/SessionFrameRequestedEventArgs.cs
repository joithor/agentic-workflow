namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>A session asked to open the frame of the workflow Step it works on.</summary>
/// <param name="address">The instance address, ITM-…/step/N.</param>
/// <param name="state">The state the Step is in, or null when the launch row has none.</param>
internal sealed class SessionFrameRequestedEventArgs(string address, string? state) : EventArgs
{
    /// <summary>The instance's address.</summary>
    public string Address { get; } = address;

    /// <summary>The Step's state, or null.</summary>
    public string? State { get; } = state;
}
