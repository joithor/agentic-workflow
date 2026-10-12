using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>The `?` sheet. Red stub.</summary>
internal sealed class ShortcutsViewModel : ObservableObject
{
    /// <summary>The sheet over the catalog. Red stub.</summary>
    /// <param name="catalog">The catalog.</param>
    internal ShortcutsViewModel(CatalogViewModel catalog)
    {
        _ = catalog;
    }

    /// <summary>True while open.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Its lines.</summary>
    public IReadOnlyList<ShortcutRow> Rows { get; private set; } = [];

    /// <summary>Opens it.</summary>
    internal void Open() => throw new NotSupportedException();

    /// <summary>Closes it.</summary>
    internal void Close() => throw new NotSupportedException();
}
