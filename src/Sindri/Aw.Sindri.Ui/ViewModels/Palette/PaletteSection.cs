namespace Aw.Sindri.Ui.ViewModels.Palette;

/// <summary>A palette section with its matching entries.</summary>
/// <param name="Heading">"Actions", "Go to" or "Sindri".</param>
/// <param name="Entries">Its entries.</param>
internal sealed record PaletteSection(string Heading, IReadOnlyList<PaletteEntry> Entries);
