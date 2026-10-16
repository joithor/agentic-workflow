namespace Aw.Sindri.Ui.Api;

/// <summary>POST /sindri/items' answer (API spec §3.2 ItemCreated, P3): the accepted command and the new item's id.</summary>
/// <param name="Accepted">The accepted command (its sequence is the next query's X-Sindri-After).</param>
/// <param name="ItemId">The new item's id.</param>
internal sealed record ItemCreatedView(CommandAcceptedView Accepted, string ItemId);
