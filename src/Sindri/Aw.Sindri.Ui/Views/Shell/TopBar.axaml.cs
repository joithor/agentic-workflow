using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell's top bar: binds to the shell, and passes the health pill's click to it.</summary>
internal sealed partial class TopBar : UserControl
{
    public TopBar()
    {
        InitializeComponent();
    }

    private void OnHealthPill(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.GoTo(PageId.Health);
}
