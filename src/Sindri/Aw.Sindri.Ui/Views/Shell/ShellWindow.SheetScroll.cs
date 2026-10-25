using Avalonia.Controls;
using Avalonia.Threading;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The sheet starts at its top (<see cref="ShellWindow"/>, continued; #1100).</summary>
internal sealed partial class ShellWindow
{
    // Each kind of sheet has its own scroll viewer, which keeps its offset while it is hidden: a sheet that closes and reopens would
    // start where it was left. Every viewer goes home as the sheet opens, and again once the new content has laid out (its extent can
    // change after the first pass). A child stacked on a frame, and Back, are subject changes of an open sheet and do not reset.
    private void ResetSheetScroll()
    {
        ScrollSheetsHome();
        Dispatcher.UIThread.Post(ScrollSheetsHome, DispatcherPriority.Loaded);
    }

    private void ScrollSheetsHome()
    {
        foreach (var scroll in this.Get<Panel>("SheetContent").Children.OfType<ScrollViewer>())
        {
            scroll.ScrollToHome();
        }
    }
}
