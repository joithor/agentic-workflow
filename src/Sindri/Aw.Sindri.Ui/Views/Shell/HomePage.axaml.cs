using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Queue;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Home's panel frames and its health strip, which passes its click to the shell.</summary>
internal sealed partial class HomePage : UserControl
{
    public HomePage()
    {
        InitializeComponent();
        AddHandler(ItemRow.OpenEvent, OnOpen);
        AddHandler(ItemRow.SelectEvent, OnSelect);
    }

    private void OnHealthStrip(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.GoTo(PageId.Health);

    // "Show all N in Queue": the Queue page, filtered to the panel's group for Running (G R), whole for Queued.
    private void OnShowAll(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell && sender is Button { Tag: string tag })
        {
            shell.GoToQueue(tag == "Running" ? QueueFilter.Running : QueueFilter.All);
        }
    }

    private void OnShowHeld(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Queue.ShowHeld(QueueScope.Home);

    private void OnPanelEntered(object? sender, Avalonia.Input.PointerEventArgs e) => (DataContext as ShellViewModel)?.Queue.Hold(holding: true);

    private void OnPanelExited(object? sender, Avalonia.Input.PointerEventArgs e) => (DataContext as ShellViewModel)?.Queue.Hold(holding: false);

    // Focus arriving on a row, or on a control in it, selects it: the selection is the focus (§3.3).
    private void OnSelect(object? sender, RoutedEventArgs e)
    {
        if (ItemRow.ItemIdOf(e) is { Length: > 0 } id && DataContext is ShellViewModel shell)
        {
            shell.Queue.Select(id, QueueScope.Home);
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (ItemRow.ItemIdOf(e) is { Length: > 0 } id && DataContext is ShellViewModel shell)
        {
            shell.Queue.Select(id, QueueScope.Home);
            shell.OpenItem(id);
        }
    }
}
