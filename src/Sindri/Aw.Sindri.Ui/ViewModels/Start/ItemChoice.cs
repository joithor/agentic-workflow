namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>One item the queue shows, offered to fill the item field.</summary>
/// <param name="Id">The item's id.</param>
/// <param name="Display">The picker's line.</param>
internal sealed record ItemChoice(string Id, string Display);
