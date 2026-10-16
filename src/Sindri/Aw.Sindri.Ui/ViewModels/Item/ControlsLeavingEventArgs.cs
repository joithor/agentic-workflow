using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Item;

/// <summary>The controls about to leave the item sheet's actions (still in them while the event runs).</summary>
/// <param name="controls">The controls that go.</param>
internal sealed class ControlsLeavingEventArgs(IReadOnlyList<ControlViewModel> controls) : EventArgs
{
    /// <summary>The controls that go.</summary>
    public IReadOnlyList<ControlViewModel> Controls { get; } = controls;
}
