namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>One item the queue shows, offered to fill the item field.</summary>
/// <param name="Id">The item's id.</param>
/// <param name="Display">The picker's line.</param>
/// <param name="Repo">The repo the item belongs to, or null.</param>
internal sealed record ItemChoice(string Id, string Display, string? Repo = null);
