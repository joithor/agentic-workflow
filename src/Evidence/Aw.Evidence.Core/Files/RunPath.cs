namespace Aw.Evidence.Core.Files;

/// <summary>Builds run-file paths without touching the file system ('/' separators; run files hold absolute paths).</summary>
public static class RunPath
{
    /// <summary>Joins a directory and a file name.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="file">The file name.</param>
    /// <returns>The path.</returns>
    public static string Combine(string directory, string file) => directory.TrimEnd('/') + "/" + file.TrimStart('/');

    /// <summary>The last segment of a path ('/runs/r1' is 'r1').</summary>
    /// <param name="path">The path.</param>
    /// <returns>The name.</returns>
    public static string NameOf(string path)
    {
        var trimmed = path.TrimEnd('/');
        return trimmed[(trimmed.LastIndexOf('/') + 1)..];
    }
}
