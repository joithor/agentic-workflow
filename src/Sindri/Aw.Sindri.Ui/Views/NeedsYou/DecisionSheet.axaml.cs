using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Views.NeedsYou;

/// <summary>The decision sheet: a press on an option asks the view model to send Decide with it.</summary>
internal sealed partial class DecisionSheet : UserControl
{
    public DecisionSheet()
    {
        InitializeComponent();
    }

    private void OnOption(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DecisionSheetViewModel sheet && sender is Control { DataContext: DecisionOption option })
        {
            _ = sheet.Choose(option.Id);
        }
    }
}
