namespace Aw.Sindri.Host.Mcp;

/// <summary>One free-text field a tool result fences.</summary>
/// <param name="Path">Where the field is: camelCase names joined by '.', a name ending in "[]" for every element of an array (groups[].items[].title; a path may end in "[]" for an array of strings).</param>
/// <param name="AuthorField">The field next to it that names who wrote the text, or null when the row has none.</param>
/// <param name="EveryString">Whether the field holds a value of any shape (a job's result) whose string values, however deep, are all fenced; numbers, booleans and nulls stay as they are.</param>
internal sealed record FenceField(string Path, string? AuthorField = null, bool EveryString = false);
