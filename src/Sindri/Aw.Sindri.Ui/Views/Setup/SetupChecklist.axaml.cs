using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Setup;

namespace Aw.Sindri.Ui.Views.Setup;

/// <summary>The setup checklist: a click only calls the view model.</summary>
internal sealed partial class SetupChecklist : UserControl
{
    public SetupChecklist()
    {
        InitializeComponent();
    }

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnRetry(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SetupViewModel setup)
        {
            await setup.Retry(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
