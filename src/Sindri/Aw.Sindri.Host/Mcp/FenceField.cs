namespace Aw.Sindri.Host.Mcp;

/// <summary>One free-text field a tool result fences.</summary>
/// <param name="Path">Where the field is: camelCase names joined by '.', a name ending in "[]" for every element of an array (groups[].items[].title).</param>
/// <param name="AuthorField">The field next to it that names who wrote the text, or null when the row has none.</param>
internal sealed record FenceField(string Path, string? AuthorField = null);
