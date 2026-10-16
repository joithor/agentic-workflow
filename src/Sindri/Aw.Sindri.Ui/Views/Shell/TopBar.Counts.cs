using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The top bar (continued): the navigation's counts, the last thing to give way at 900 px (UI spec §3.2).</summary>
internal sealed partial class TopBar
{
    // What the shown counts take, remembered while they show so the bar knows what hiding them would give back.
    private double CountsWidth { get; set; }

    private ListBox NavigationList => this.Get<ListBox>("Navigation");

    private IEnumerable<TextBlock> CountBlocks => NavigationList.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("nav-count"));

    private void RememberCounts()
    {
        if (_fit < Fit.NoCounts)
        {
            CountsWidth = CountBlocks.Where(t => t.IsVisible).Sum(t => t.DesiredSize.Width + SpacingBeforeCount);
        }
    }

    // The navigation's width with its counts, which is what every step but the last measures.
    private double NavigationWidthWithCounts => NavSegments.DesiredSize.Width + (_fit == Fit.NoCounts ? CountsWidth : 0);

    // The count sits after its label in a stack spaced by 6.
    private const double SpacingBeforeCount = 6;
}
