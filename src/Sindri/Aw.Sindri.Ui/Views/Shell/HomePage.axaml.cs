using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Home's panel frames and its health strip, which passes its click to the shell.</summary>
internal sealed partial class HomePage : UserControl
{
    public HomePage()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnHealthStrip(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.GoTo(PageId.Health);
}
