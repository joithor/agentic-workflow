using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Facts;

namespace Aw.Sindri.Ui.Views.Facts;

/// <summary>A link fact's viewer: Open hands the link to the platform.</summary>
internal sealed partial class LinkFactView : UserControl
{
    public LinkFactView()
    {
        InitializeComponent();
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (DataContext is LinkFactViewer { CanOpen: true } viewer && Uri.TryCreate(viewer.Url, UriKind.Absolute, out var uri))
        {
            _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(uri);
        }
    }
}
