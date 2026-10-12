using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>One palette entry. Red stub.</summary>
/// <param name="id">Its id.</param>
internal sealed class PaletteEntry(string id) : ObservableObject
{
    /// <summary>Its id.</summary>
    public string Id { get; } = id;

    /// <summary>Its label.</summary>
    public string Label { get; } = id;

    /// <summary>Its status.</summary>
    public string Status { get; } = id;

    /// <summary>Its key.</summary>
    public string Key { get; } = id;

    /// <summary>Its accessible name.</summary>
    public string Name { get; } = id;

    /// <summary>True for a catalog row.</summary>
    public bool IsAction { get; }

    /// <summary>True when it runs.</summary>
    public bool IsEnabled { get; }

    /// <summary>True when selected.</summary>
    public bool IsSelected { get; }

    /// <summary>Its control.</summary>
    public ControlViewModel? Control { get; }
}
