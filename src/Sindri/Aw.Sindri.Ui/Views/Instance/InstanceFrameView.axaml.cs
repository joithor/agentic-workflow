using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Instance;

/// <summary>The instance frame inside the sheet: Back closes the sheet like Esc, and Open in hands the subject's link to the platform.</summary>
internal sealed partial class InstanceFrameView : UserControl
{
    public InstanceFrameView()
    {
        InitializeComponent();
    }

    private void OnBack(object? sender, RoutedEventArgs e) => (TopLevel.GetTopLevel(this)?.DataContext as ShellViewModel)?.CloseSheet();

    private void OnOpenIn(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstanceFrameViewModel { Subject: { HasHref: true } subject } && Uri.TryCreate(subject.Href, UriKind.Absolute, out var uri))
        {
            _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(uri);
        }
    }
}
