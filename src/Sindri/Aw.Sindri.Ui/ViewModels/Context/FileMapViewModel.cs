using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The file map on the Context page (U9d owns this class); a seam until then.</summary>
internal sealed class FileMapViewModel(ContextPorts ports) : ObservableObject
{
    /// <summary>What the stage reads through.</summary>
    internal ContextPorts Ports { get; } = ports;

    /// <summary>The symbols column of the file map last read (S-9); null before a read.</summary>
    internal SymbolsColumnViewModel? Symbols { get; private set; }

    /// <summary>Takes the symbols object of a file map answer into the column.</summary>
    /// <param name="view">The file map answer.</param>
    internal void Show(FileMapView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        Symbols = new SymbolsColumnViewModel(view.Symbols);
    }
}
