using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Palette;
using Aw.Sindri.Ui.Views.Palette;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window (continued): the palette and the `?` sheet's keys and focus (UI spec §8, §11.2).</summary>
internal sealed partial class ShellWindow
{
    // ⌘K opens the palette, `?` the shortcuts sheet (never while typing in a field, nor over the palette), ⌘, Settings.
    private bool OpenByKey(KeyEventArgs e)
    {
        if (Shell is not { IsChromeVisible: true } shell)
        {
            return false;
        }

        if (e.Key == Key.K && e.KeyModifiers == _command)
        {
            shell.Shortcuts.Close();   // one overlay at a time; focus goes back to the original opener first (OnOverlayChanged)
            shell.Palette.Open();
            return true;
        }

        if (IsQuestionMark(e) && e.Source is not TextBox && !shell.Palette.IsOpen)
        {
            shell.Shortcuts.Open();
            return true;
        }

        if (e.Key != Key.OemComma || e.KeyModifiers != _command)
        {
            return false;
        }

        shell.GoTo(PageId.Settings);
        // Like the sheet's heading (§11.2), opening Settings moves focus into it: its first control.
        Dispatcher.UIThread.Post(() => FirstFocusable(this.Get<Control>("SettingsSlot"))?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        return true;
    }

    private bool OverlayOpen => Shell is { } shell && (shell.Palette.IsOpen || shell.Shortcuts.IsOpen);

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
