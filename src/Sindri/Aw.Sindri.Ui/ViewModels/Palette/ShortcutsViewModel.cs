using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>
/// The `?` sheet (UI spec §8): §8's fixed keys, then each catalog row's own shortcut (E-8) with its label. It is
/// read when it opens, so a catalog read since shows.
/// </summary>
/// <param name="catalog">The catalog, for the rows' shortcuts.</param>
internal sealed class ShortcutsViewModel(CatalogViewModel catalog) : ObservableObject
{
    /// <summary>The sheet's title and accessible name.</summary>
    public string Title { get; } = Labels.Shortcuts;

    /// <summary>The closing hint.</summary>
    public string Hint { get; } = Labels.EscCloses;

    /// <summary>True while open.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>Its lines: the fixed keys, then the catalog's.</summary>
    public IReadOnlyList<ShortcutRow> Rows { get; private set => Set(ref field, value); } = [];

    /// <summary>Opens it.</summary>
    internal void Open()
    {
        Rows =
        [
            .. Labels.FixedKeys.Select(k => new ShortcutRow(k.Keys, k.Action)),
            .. catalog.Controls.Where(c => c.Shortcut.Length > 0).Select(c => new ShortcutRow(c.Shortcut, c.Label)),
        ];
        IsOpen = true;
    }

    /// <summary>Closes it (Esc).</summary>
    internal void Close() => IsOpen = false;
}
