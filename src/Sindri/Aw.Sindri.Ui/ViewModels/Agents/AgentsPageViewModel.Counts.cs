namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The page's tab count (<see cref="AgentsPageViewModel"/>, continued; U11a T5).</summary>
internal sealed partial class AgentsPageViewModel
{
    /// <summary>Sets the tab's count text; the shell's counts partial calls it with the status' live figure, or empty when none is served.</summary>
    /// <param name="live">The text.</param>
    internal void ShowNavCount(string live) => NavCount = live;
}
