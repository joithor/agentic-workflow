using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Item;

/// <summary>The item detail inside the shell's sheet; Load more asks for the timeline's next page.</summary>
internal sealed partial class ItemSheet : UserControl
{
    private ItemSheetViewModel? _sheet;

    public ItemSheet()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _sheet?.ControlsLeaving -= OnControlsLeaving;
        _sheet = DataContext as ItemSheetViewModel;
        _sheet?.ControlsLeaving += OnControlsLeaving;
    }

    // L8: a control that holds the focus is about to go (its action is no longer allowed): focus moves to the sheet's title
    // first, so it is never left on nothing.
    private void OnControlsLeaving(object? sender, ControlsLeavingEventArgs leaving)
    {
        if (TopLevel.GetTopLevel(this) is not Window window || window.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return;
        }

        if (focused.FindAncestorOfType<CommandControl>(includeSelf: true)?.DataContext is ControlViewModel owner && leaving.Controls.Contains(owner))
        {
            _ = window.FindControl<TextBlock>("SheetTitle")?.Focus(NavigationMethod.Tab);
        }
    }

    private void OnLoadMore(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ItemSheetViewModel sheet)
        {
            _ = LoadMoreAndFocus(sheet);
        }
    }

    // L12: the page lands and the button may hide (the last page): focus goes to the first new row, never to nothing.
    private async Task LoadMoreAndFocus(ItemSheetViewModel sheet)
    {
        var before = sheet.Timeline.Count;
        _ = await sheet.LoadMore(CancellationToken.None).ConfigureAwait(true);
        if (sheet.Timeline.Count > before)
        {
            Dispatcher.UIThread.Post(() => FocusRow(before), DispatcherPriority.Loaded);
        }
    }

    private void FocusRow(int index)
    {
        var rows = this.FindControl<ItemsControl>("TimelineList")?.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("timeline-row")).ToList();
        if (rows is { } && index < rows.Count)
        {
            _ = rows[index].Focus(NavigationMethod.Tab);
        }
    }
}
