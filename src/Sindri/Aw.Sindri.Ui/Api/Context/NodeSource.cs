namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Where a node came from.</summary>
/// <param name="Kind">Kind, as served ('ticket', 'doc', 'file', ...).</param>
/// <param name="Ref">The id or path, as served.</param>
/// <param name="Title">Title, as served, or null.</param>
/// <param name="Url">Url, as served, or null.</param>
internal sealed record NodeSource(string Kind, string Ref, string? Title = null, string? Url = null);
