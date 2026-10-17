using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Facts;
using Aw.Sindri.Ui.Views.Facts;

namespace Aw.Sindri.Ui.Views.Instance;

/// <summary>The facts panel: View opens or closes a row's viewer (focus stays on the button), Copy puts a pending output's CLI on the clipboard.</summary>
internal sealed partial class FactsPanel : UserControl
{
    public FactsPanel()
    {
        InitializeComponent();
        DataTemplates.Add(FactViewTemplates.Template);   // an open viewer is drawn by the view registered for its type
    }

    private void OnView(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: FactRowViewModel row })
        {
            row.ToggleView();
        }
    }

    private void OnCopyCli(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: FactRowViewModel row })
        {
            _ = row.CopyCli(CancellationToken.None);
        }
    }
}
