namespace Aw.BugFix.Core.Model;

/// <summary>A clean work tree a step runs in: where it is, and what it holds.</summary>
/// <param name="Root">The work tree's root (real path).</param>
/// <param name="Directory">The step's directory, relative to the root ("" for the root).</param>
/// <param name="Head">The commit checked out.</param>
/// <param name="Untracked">Its untracked files (not ignored ones): repository-relative path → SHA-256.</param>
public sealed record Checkout(string Root, string Directory, string Head, IReadOnlyDictionary<string, string> Untracked);
