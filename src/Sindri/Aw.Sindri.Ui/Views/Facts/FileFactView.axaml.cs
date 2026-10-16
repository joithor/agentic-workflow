using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.Views.Facts;

/// <summary>A file fact's viewer: Open asks the platform to open the path the fact serves; the viewer says when it could not.</summary>
internal sealed partial class FileFactView : UserControl
{
    public FileFactView()
    {
        InitializeComponent();
    }

    private async void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not FileFactViewer viewer || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var file = Path.IsPathRooted(viewer.Path) ? await top.StorageProvider.TryGetFileFromPathAsync(new Uri(viewer.Path)).ConfigureAwait(true) : null;
        viewer.ReportOpened(file is not null && await top.Launcher.LaunchFileAsync(file).ConfigureAwait(true));
    }
}
