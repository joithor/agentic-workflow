using Aw.Judge.Core.Ports;

namespace Aw.Judge.Infrastructure.Processes;

/// <summary>IExecutableLocator over PATH: a regular file with an execute bit (any file on Windows) in a PATH directory.</summary>
/// <param name="environment">Reads PATH (each call, as the TS judge reads process.env).</param>
internal sealed class PathExecutableLocator(Func<string, string?> environment) : IExecutableLocator
{
    private const UnixFileMode AnyExecute = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;

    /// <inheritdoc />
    public bool IsOnPath(string name)
    {
        var directories = (environment("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        return directories.Any(directory => IsExecutable(Path.Combine(directory, name)));
    }

    private static bool IsExecutable(string file)
    {
        if (!File.Exists(file))
        {
            return false;
        }

        return OperatingSystem.IsWindows() || (File.GetUnixFileMode(file) & AnyExecute) != UnixFileMode.None;
    }
}
