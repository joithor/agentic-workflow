using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus when the session sheet closes (<see cref="ShellWindow"/>, continued; U11a): Esc returns it to the Agents row that opened the sheet.</summary>
internal sealed partial class ShellWindow
{
    // The first visible, focusable control of the page whose data is the row for the session that opened the sheet (the row's own frame, outermost first).
    private Control? OpenerSessionFrame() => Shell?.OpenerSession is { } reference
        ? Usable(this.Get<Border>("Page").GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(c => c is { Focusable: true, IsEffectivelyVisible: true, DataContext: ISessionRef row } && string.Equals(row.Ref, reference, StringComparison.Ordinal)))
        : null;
}
