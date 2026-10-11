using Aw.Kernel;

namespace Aw.BugFix.Core.Ports;

/// <summary>The file system, as the bug-fix steps need it.</summary>
public interface IFiles
{
    /// <summary>The full path of <paramref name="path"/>, resolved against <paramref name="directory"/> when relative.</summary>
    /// <param name="directory">The base directory.</param>
    /// <param name="path">A relative or full path.</param>
    /// <returns>The full path (not resolved through symlinks).</returns>
    public string FullPath(string directory, string path);

    /// <summary>The real path of an existing file (symlinks resolved), or null when nothing is there.</summary>
    /// <param name="path">A full path.</param>
    /// <returns>The real path, or null.</returns>
    public string? RealPath(string path);

    /// <summary>Whether a file or a directory exists at <paramref name="path"/>.</summary>
    /// <param name="path">A full path.</param>
    /// <returns>True when something is there.</returns>
    public bool Exists(string path);

    /// <summary>Reads a text file.</summary>
    /// <param name="path">A full path.</param>
    /// <returns>The text, or AW-BUGFIX-004.</returns>
    public Result<string> ReadText(string path);

    /// <summary>The SHA-256 (lower-case hex) of a file's bytes.</summary>
    /// <param name="path">A full path.</param>
    /// <returns>The hash, or AW-BUGFIX-004.</returns>
    public Result<string> Sha256(string path);
}
