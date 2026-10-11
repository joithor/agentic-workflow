namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// Files and directories only the user can open: 0700 directories and 0600 files on Unix. On Windows the user's profile
/// ACLs already keep them to the user, so the modes are skipped there.
/// </summary>
internal static class OwnerOnly
{
    private const UnixFileMode ReadWrite = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private const UnixFileMode ReadWriteSearch = ReadWrite | UnixFileMode.UserExecute;

    /// <summary>Creates <paramref name="path"/> (and missing parents) owner-only, and makes an existing one owner-only.</summary>
    /// <param name="path">The directory, one the app owns.</param>
    internal static void CreateDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(path);
            return;
        }

        _ = Directory.CreateDirectory(path, ReadWriteSearch);
        File.SetUnixFileMode(path, ReadWriteSearch);
    }

    /// <summary>Opens a file, creating it 0600 if it doesn't exist.</summary>
    /// <param name="path">The file.</param>
    /// <param name="mode">How to open it.</param>
    /// <param name="share">What other opens it allows (<see cref="FileShare.None"/> also takes the file's lock on Unix).</param>
    /// <returns>The stream.</returns>
    internal static FileStream Open(string path, FileMode mode, FileShare share)
    {
        var options = new FileStreamOptions { Mode = mode, Access = FileAccess.ReadWrite, Share = share };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = ReadWrite;
        }

        return new FileStream(path, options);
    }

    /// <summary>Makes an existing file 0600 (a socket, which bind creates with the process's umask).</summary>
    /// <param name="path">The file.</param>
    internal static void Restrict(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, ReadWrite);
        }
    }
}
