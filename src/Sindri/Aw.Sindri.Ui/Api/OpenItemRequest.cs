namespace Aw.Sindri.Ui.Api;

/// <summary>POST /sindri/items/{itemId}/open's body (OpenItem, desk, API P10a): the mode and the version the human saw.</summary>
/// <param name="Mode">'attach' (the only mode the app sends here).</param>
/// <param name="ItemVersion">The item's version as the app read it.</param>
internal sealed record OpenItemRequest(string Mode, long ItemVersion);
