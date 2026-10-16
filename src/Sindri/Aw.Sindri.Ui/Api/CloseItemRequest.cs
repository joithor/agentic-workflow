namespace Aw.Sindri.Ui.Api;

/// <summary>POST /sindri/items/{itemId}/close's body (API spec §3.2 CloseItem, desk, P3): the reason and the version the human saw.</summary>
/// <param name="Reason">'done' or 'cancelled'.</param>
/// <param name="ItemVersion">The item's version as the sheet showed it.</param>
internal sealed record CloseItemRequest(string Reason, long ItemVersion);
