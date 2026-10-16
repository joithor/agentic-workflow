using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.Views.Facts;

/// <summary>A file fact's viewer: Open asks the launcher to open the path the fact serves; the viewer says when it could not.</summary>
internal sealed partial class FileFactView : UserControl
{
    public FileFactView()
    {
        InitializeComponent();
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FileFactViewer viewer)
        {
            _ = viewer.Open(CancellationToken.None);
        }
    }
}
