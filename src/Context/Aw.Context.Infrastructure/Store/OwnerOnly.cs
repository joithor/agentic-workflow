namespace Aw.Context.Infrastructure.Store;

/// <summary>Owner-only permissions for the store (spec §3): directories 0700, files 0600.</summary>
internal static class OwnerOnly
{
    private const UnixFileMode DirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode FileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <summary>Creates <paramref name="path"/> if needed and makes it 0700.</summary>
    /// <param name="path">The directory.</param>
    internal static void EnsureDirectory(string path)
    {
        _ = Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, DirectoryMode);
        }
    }

    /// <summary>Makes each existing file 0600.</summary>
    /// <param name="paths">The files.</param>
    internal static void RestrictFiles(params string[] paths)
    {
        foreach (var path in paths.Where(File.Exists))
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, FileMode);
            }
        }
    }
}
