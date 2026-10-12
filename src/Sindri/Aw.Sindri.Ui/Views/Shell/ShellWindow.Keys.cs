using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Sindri.Ui.Views.Item;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window (continued): the page's single keys (UI spec §8), passed to the shell view model.</summary>
internal sealed partial class ShellWindow
{
    // §8: single keys never fire while typing in a field, over a layer, or with a modifier other than Shift ('.').
    private bool PageKey(KeyEventArgs e)
    {
        if (Shell is not { IsChromeVisible: true, IsNavigationEnabled: true } shell || e.Source is TextBox
            || shell.Palette.IsOpen || shell.Shortcuts.IsOpen || shell.Create.IsOpen
            || (e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None)
        {
            return false;
        }

        var key = KeyName(e);
        if (key is null || !shell.Press(key))
        {
            return false;
        }

        if (shell.Create.IsOpen)
        {
            Dispatcher.UIThread.Post(() => this.Get<CreateItemDialog>("CreateItemDialog").FocusTitle(), DispatcherPriority.Loaded);
        }

        return true;
    }

    // The key as the view model names it: a letter (no Shift), "Enter" or ".".
    private static string? KeyName(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            return "Enter";
        }

        if (e.Key == Key.OemPeriod)
        {
            return ".";
        }

        return e.Key is >= Key.A and <= Key.Z && e.KeyModifiers == KeyModifiers.None ? e.Key.ToString() : null;
    }

    // Esc's U2 layers, above the sheet (§8 "the topmost layer"): the create form, then a destructive control's question.
    private static bool EscapeItemLayer(Aw.Sindri.Ui.ViewModels.Shell.ShellViewModel shell)
    {
        if (shell.Create.IsOpen)
        {
            shell.Create.Close();
            return true;
        }

        if (shell.Item.Actions.FirstOrDefault(a => a.IsConfirming) is not { } asking)
        {
            return false;
        }

        asking.Keep();
        return true;
    }
}
