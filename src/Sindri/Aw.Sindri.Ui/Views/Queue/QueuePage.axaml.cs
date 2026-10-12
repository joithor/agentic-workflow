using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Queue;

/// <summary>The Queue page; a row's title click opens its sheet, and Create item opens the form.</summary>
internal sealed partial class QueuePage : UserControl
{
    public QueuePage()
    {
        InitializeComponent();
        AddHandler(ItemRow.OpenEvent, OnOpen);
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (ItemRow.ItemIdOf(e) is { Length: > 0 } id && DataContext is ShellViewModel shell)
        {
            shell.Queue.Select(id);
            shell.OpenItem(id);
        }
    }

    private void OnCreate(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Create.Open();

    private void OnShowHeld(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Queue.ShowHeld();

    private void OnPanelEntered(object? sender, Avalonia.Input.PointerEventArgs e) => (DataContext as ShellViewModel)?.Queue.Hold(holding: true);

    private void OnPanelExited(object? sender, Avalonia.Input.PointerEventArgs e) => (DataContext as ShellViewModel)?.Queue.Hold(holding: false);
}
