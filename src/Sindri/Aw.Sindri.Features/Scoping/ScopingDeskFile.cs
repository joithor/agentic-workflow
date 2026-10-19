namespace Aw.Sindri.Features.Scoping;

/// <summary>A file a Step writes into the run area.</summary>
/// <param name="Path">The path relative to the run area, with '/' separators.</param>
/// <param name="Content">The text.</param>
/// <param name="Append">True to append the content (rounds.jsonl); false to replace the file.</param>
public sealed record ScopingDeskFile(string Path, string Content, bool Append);
