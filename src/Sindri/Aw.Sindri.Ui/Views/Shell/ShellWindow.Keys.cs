using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Views.Item;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window (continued): the page's single keys (UI spec §8), passed to the shell view model.</summary>
internal sealed partial class ShellWindow
{
    // §8: single keys never fire while typing in a field, over a layer, or with a modifier other than Shift ('.').
    private bool PageKey(KeyEventArgs e)
    {
        if (Shell is not { } shell || KeysBlocked(shell, e) || KeyName(e) is not { } key)
        {
            return false;
        }

        if (key == "Enter" && EnterIntoSheet(shell))
        {
            return true;
        }

        if (!shell.Press(key, inSheet: FocusInSheet()))
        {
            return false;
        }

        FocusOpenedField(shell, key);
        return true;
    }

    private static bool KeysBlocked(ViewModels.Shell.ShellViewModel shell, KeyEventArgs e) =>
        shell is not { IsChromeVisible: true, IsNavigationEnabled: true } || e.Source is TextBox
        || shell.Palette.IsOpen || shell.Shortcuts.IsOpen || shell.Create.IsOpen
        || (e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None;

    // §11.2: with the sheet beside the page and open on the selected row's item (the row drawn on this page), Enter again moves into its first action
    // (its heading when it has none). Covering the page, focus is already in the sheet.
    private bool EnterIntoSheet(ViewModels.Shell.ShellViewModel shell)
    {
        if (!shell.IsSheetShown || Covering || FocusInSheet() || shell.Queue.IsGuarded || !shell.IsSelectedRowOpen)
        {
            return false;
        }

        var action = SheetPanel.GetVisualDescendants().OfType<Controls.CommandControl>().FirstOrDefault(c => c.IsEffectivelyVisible);
        if (action?.FocusRun() != true)
        {
            _ = this.Get<Control>("SheetTitle").Focus(NavigationMethod.Tab);   // no action, or none takes focus (pending): the heading
        }

        return true;
    }

    // §11.2 "Enter again (or Tab)": Tab from the last control of the selected row whose item the sheet shows (beside the page)
    // goes into the sheet. Tab within the row is unchanged. This handler runs before the focus manager's, which ignores a handled key.
    private bool TabOutOfOpenRow(KeyEventArgs e)
    {
        if (e is not { Key: Key.Tab, KeyModifiers: KeyModifiers.None } || Shell is not { IsSelectedRowOpen: true } shell || AnyLayerOpen(shell)
            || FocusManager?.GetFocusedElement() is not Visual focused || OpenRowFrame(focused) is not { } frame)
        {
            return false;
        }

        var next = FocusManager.FindNextElement(NavigationDirection.Next) as Visual;
        return (next is null || !frame.IsVisualAncestorOf(next)) && EnterIntoSheet(shell);
    }

    private static bool AnyLayerOpen(ViewModels.Shell.ShellViewModel shell) => shell.Palette.IsOpen || shell.Shortcuts.IsOpen || shell.Create.IsOpen;

    // The frame of the row that the focused control is in, when that row is the one the sheet shows.
    private Border? OpenRowFrame(Visual focused) =>
        focused.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal))
            is { DataContext: ViewModels.Queue.ItemRowViewModel row } frame && string.Equals(row.Id, Shell?.Item.ItemId, StringComparison.Ordinal) ? frame : null;

    // §8, §11.2: keys belong to the focused region; the row keys are the page's, not the sheet's.
    private bool FocusInSheet() => FocusManager?.GetFocusedElement() is Visual focused && (SheetPanel == focused || SheetPanel.IsVisualAncestorOf(focused));

    // A key that opened a field moves focus into it: C the create form's title, / the Queue page's filter.
    private void FocusOpenedField(ViewModels.Shell.ShellViewModel shell, string key)
    {
        if (shell.Create.IsOpen)
        {
            Dispatcher.UIThread.Post(() => this.Get<CreateItemDialog>("CreateItemDialog").FocusTitle(), DispatcherPriority.Loaded);
        }
        else if (key == "/" && shell.Queue.IsFiltering)
        {
            Dispatcher.UIThread.Post(() => _ = this.Get<TextBox>("QueueFilter").Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        }
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

        if (e.Key == Key.OemQuestion && e.KeyModifiers == KeyModifiers.None)
        {
            return "/";
        }

        return e.Key is >= Key.A and <= Key.Z && e.KeyModifiers == KeyModifiers.None ? e.Key.ToString() : null;
    }

    // Esc's U2 layers, above the sheet (§8 "the topmost layer"): the create form, then a destructive control's question.
    private bool EscapeItemLayer(ViewModels.Shell.ShellViewModel shell)
    {
        if (shell.Create.IsOpen)
        {
            shell.Create.Close();
            return true;
        }

        if (shell.Item.Actions.FirstOrDefault(a => a.IsConfirming) is { } asking)
        {
            asking.Keep();
            // L11: the question goes and focus returns to the control that asked it (the same path as a click on Keep).
            _ = this.GetVisualDescendants().OfType<Controls.CommandControl>().FirstOrDefault(c => ReferenceEquals(c.DataContext, asking))?.FocusRun();
            return true;
        }

        // §8: the filter is the last layer, after the sheet.
        if (shell.IsSheetShown || !shell.Queue.IsFiltering)
        {
            return false;
        }

        shell.Queue.ClearFilter();
        return true;
    }
}
