namespace Aw.Context.Core.Extract;

/// <summary>One file of a repo at a commit, as the extractor reads it.</summary>
/// <param name="Path">The path from the repo root, with '/' separators.</param>
/// <param name="Text">The file's text.</param>
public sealed record RepoFile(string Path, string Text);
