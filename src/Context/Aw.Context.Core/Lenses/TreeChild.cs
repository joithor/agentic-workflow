namespace Aw.Context.Core.Lenses;

/// <summary>One direct child of a directory and the range of files under it.</summary>
/// <param name="Name">Its name, without a trailing '/'.</param>
/// <param name="Path">Its path from the root, with a trailing '/' for a directory.</param>
/// <param name="IsDirectory">Whether it is a directory.</param>
/// <param name="Start">The first file's index in the tree.</param>
/// <param name="End">One past the last.</param>
internal sealed record TreeChild(string Name, string Path, bool IsDirectory, int Start, int End);
