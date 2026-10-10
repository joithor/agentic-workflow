namespace Aw.Readiness.Core.Ports;

/// <summary>A read-only view of one repo checkout. Paths are relative to the repo root and use '/'.</summary>
public interface IRepoTree
{
    /// <summary>Whether a file or directory exists at the path.</summary>
    /// <param name="path">A repo-relative path.</param>
    /// <returns>True when it exists.</returns>
    public bool Exists(string path);

    /// <summary>A file's text, or null when it is missing or can't be read.</summary>
    /// <param name="path">A repo-relative file path.</param>
    /// <returns>The text, or null.</returns>
    public string? ReadText(string path);

    /// <summary>Every file under a directory (recursively) whose name ends with the suffix, sorted ordinally. Build
    /// output ('bin', 'obj'), 'node_modules' and dot-directories are skipped.</summary>
    /// <param name="directory">A repo-relative directory ("" for the root).</param>
    /// <param name="suffix">A name suffix, such as ".csproj".</param>
    /// <returns>Repo-relative paths; empty when the directory is missing.</returns>
    public IReadOnlyList<string> Files(string directory, string suffix);
}
