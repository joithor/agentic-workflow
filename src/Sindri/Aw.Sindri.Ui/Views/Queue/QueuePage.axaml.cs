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
        if (e.Source is ItemRow { ItemId.Length: > 0 } row && DataContext is ShellViewModel shell)
        {
            shell.Queue.Select(row.ItemId);
            shell.OpenItem(row.ItemId);
        }
    }

    private void OnCreate(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Create.Open();
}
