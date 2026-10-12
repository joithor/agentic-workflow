using Avalonia.Controls;
using Avalonia.Input;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Views.Palette;

/// <summary>The `?` sheet's view (UI spec §8): a click on the scrim closes it; the window handles Esc and focus.</summary>
internal sealed partial class ShortcutsSheet : UserControl
{
    public ShortcutsSheet()
    {
        InitializeComponent();
    }

    /// <summary>Moves focus onto the sheet (the window calls it when the sheet opens).</summary>
    internal void FocusSheet() => ShortcutsCard.Focus(NavigationMethod.Tab);

    private void OnScrim(object? sender, PointerPressedEventArgs e) => (DataContext as ShortcutsViewModel)?.Close();
}
