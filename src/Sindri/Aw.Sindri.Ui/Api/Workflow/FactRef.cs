namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A fact's reference.</summary>
/// <param name="Name">Name, as served.</param>
/// <param name="Size">Size, as served.</param>
/// <param name="Hash">Hash, as served.</param>
/// <param name="Path">Path, as served.</param>
/// <param name="ItemId">ItemId, as served.</param>
/// <param name="Url">Url, as served.</param>
/// <param name="Host">Host, as served.</param>
/// <param name="Title">Title, as served.</param>
/// <param name="FactKey">FactKey, as served.</param>
internal sealed record FactRef(
    string? Name,
    long? Size,
    string? Hash,
    string? Path,
    string? ItemId,
    string? Url,
    string? Host,
    string? Title,
    string? FactKey);
