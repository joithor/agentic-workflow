namespace Aw.Sindri.Ui.Api;

/// <summary>POST /sindri/items' body (API spec §3.2 CreateItem, desk, P3).</summary>
/// <param name="Title">The title.</param>
/// <param name="Repo">The repo, or null for the API's default.</param>
/// <param name="Body">The body text, or null.</param>
/// <param name="Link">A tracker record key, or null (the app's form doesn't set one).</param>
internal sealed record CreateItemRequest(string Title, string? Repo, string? Body, string? Link);
