namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>The per-type handle of a fact (E-20).</summary>
/// <param name="Name">A file's name.</param>
/// <param name="Size">A file's size in bytes.</param>
/// <param name="Hash">A file's hash.</param>
/// <param name="Path">A file's path.</param>
/// <param name="ItemId">An item's id.</param>
/// <param name="Url">A link's URL.</param>
/// <param name="Host">A link's host.</param>
/// <param name="Title">A link's title.</param>
/// <param name="FactKey">The key the viewers pass to GetInstanceFact.</param>
public sealed record FactRef(
    string? Name,
    long? Size,
    string? Hash,
    string? Path,
    string? ItemId,
    string? Url,
    string? Host,
    string? Title,
    string? FactKey);
