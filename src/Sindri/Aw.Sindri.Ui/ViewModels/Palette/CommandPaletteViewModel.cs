using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>The command palette (UI spec §8). Red stub.</summary>
internal sealed class CommandPaletteViewModel : ObservableObject
{
    /// <summary>The palette over the catalog. Red stub.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="goTo">Shows a page.</param>
    /// <param name="remember">Records an entry run.</param>
    internal CommandPaletteViewModel(CatalogViewModel catalog, Action<PageId> goTo, Action<string> remember)
    {
        _ = (catalog, goTo, remember);
    }

    /// <summary>True while open.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The typed query.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>The sections with matching entries.</summary>
    public IReadOnlyList<PaletteSection> Sections { get; private set; } = [];

    /// <summary>Every matching entry, in order.</summary>
    public IReadOnlyList<PaletteEntry> Entries { get; private set; } = [];

    /// <summary>The selected entry.</summary>
    public PaletteEntry? Selected { get; private set; }

    /// <summary>Opens it.</summary>
    internal void Open() => throw new NotSupportedException();

    /// <summary>Closes it.</summary>
    internal void Close() => throw new NotSupportedException();

    /// <summary>Moves the selection.</summary>
    /// <param name="by">How far.</param>
    internal void Move(int by) => throw new NotSupportedException();

    /// <summary>Runs an entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">Cancels it.</param>
    /// <returns>A task.</returns>
    internal Task Run(PaletteEntry? entry, CancellationToken cancellationToken) => throw new NotSupportedException();
}
