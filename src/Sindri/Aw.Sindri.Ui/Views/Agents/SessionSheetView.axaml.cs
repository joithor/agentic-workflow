using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Agents;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>The session sheet inside the shell's sheet: Copy puts the pending Focus's CLI on the clipboard.</summary>
internal sealed partial class SessionSheetView : UserControl
{
    public SessionSheetView()
    {
        InitializeComponent();
    }

    private void OnCopyFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SessionSheetViewModel sheet)
        {
            _ = sheet.CopyFocus(CancellationToken.None);
        }
    }
}
