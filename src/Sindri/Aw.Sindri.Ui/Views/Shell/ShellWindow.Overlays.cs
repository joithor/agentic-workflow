using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window (continued): the palette and the `?` sheet's keys and focus (UI spec §8, §11.2).</summary>
internal sealed partial class ShellWindow
{
    // `?` is Shift+/ on most layouts; the key symbol says so on the rest.
    private static bool IsQuestionMark(KeyEventArgs e) =>
        e.KeySymbol == "?" || (e.Key == Key.OemQuestion && e.KeyModifiers == KeyModifiers.Shift);

    // The palette or the ? sheet opened: remember what had focus and move focus into it. Closed: give focus back to what
    // had it (the ⌘K field, a page), or the ⌘K field when that is gone.
    private void OnOverlayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommandPaletteViewModel.IsOpen) || Shell is not { } shell)
        {
            return;
        }

        var open = sender == shell.Palette ? shell.Palette.IsOpen : shell.Shortcuts.IsOpen;
        if (open)
        {
            _overlayOpener ??= FocusManager?.GetFocusedElement() as Control;
            Dispatcher.UIThread.Post(() => FocusOverlay(sender == shell.Palette), DispatcherPriority.Loaded);
            return;
        }

        var back = Usable(_overlayOpener) ?? Usable(TopBar.Get<Control>("SearchField"));
        _overlayOpener = null;
        _ = back?.Focus(NavigationMethod.Tab);
    }

    private void FocusOverlay(bool palette)
    {
        if (palette)
        {
            this.Get<CommandPalette>("CommandPalette").FocusQuery();
        }
        else
        {
            this.Get<ShortcutsSheet>("ShortcutsSheet").FocusSheet();
        }
    }
}
