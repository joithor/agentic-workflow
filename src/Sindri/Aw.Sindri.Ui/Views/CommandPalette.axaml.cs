using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Views;

/// <summary>
/// The command palette's view (UI spec §8): keys only. ↑/↓ move the selection, Enter runs it and Esc closes, caught
/// before the query field sees them; a click on an entry runs it and a click on the scrim closes. Where focus goes when
/// it opens and closes is the window's (ShellWindow).
/// </summary>
internal sealed partial class CommandPalette : UserControl
{
    private CommandPaletteViewModel? _palette;

    public CommandPalette()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        _palette?.PropertyChanged -= OnPaletteChanged;
        _palette = Palette;
        _palette?.PropertyChanged += OnPaletteChanged;
        base.OnDataContextChanged(e);
    }

    private CommandPaletteViewModel? Palette => DataContext as CommandPaletteViewModel;

    /// <summary>Moves focus into the query field (the window calls it when the palette opens).</summary>
    internal void FocusQuery() => PaletteQuery.Focus(NavigationMethod.Tab);

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (Palette is not { } palette)
        {
            return;
        }

        if (e.Key is Key.Down or Key.Up)
        {
            palette.Move(e.Key == Key.Down ? 1 : -1);
        }
        else if (e.Key == Key.Enter)
        {
            _ = palette.Run(palette.Selected, CancellationToken.None);
        }
        else if (e.Key == Key.Escape)
        {
            palette.Close();
        }
        else
        {
            return;
        }

        e.Handled = true;
    }

    private void OnEntry(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PaletteEntry entry })
        {
            _ = Palette?.Run(entry, CancellationToken.None);
        }
    }

    private void OnScrim(object? sender, PointerPressedEventArgs e) => Palette?.Close();

    // The selection stays in view as the arrows move it (after the list has laid out its new entries).
    private void OnPaletteChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandPaletteViewModel.Selected))
        {
            Dispatcher.UIThread.Post(
                () => PaletteSections.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.DataContext == _palette?.Selected)?.BringIntoView(),
                DispatcherPriority.Loaded);
        }
    }
}
