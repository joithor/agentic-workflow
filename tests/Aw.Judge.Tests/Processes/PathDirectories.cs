using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Processes;

/// <summary>Two temporary PATH directories, deleted on dispose; the PATH value lists them with an empty entry between.</summary>
internal sealed class PathDirectories : IDisposable
{
    public PathDirectories()
    {
        First = Directory.CreateTempSubdirectory("judge-path-a-").FullName;
        Second = Directory.CreateTempSubdirectory("judge-path-b-").FullName;
    }

    public string First { get; }

    public string Second { get; }

    public string Path => $"{First}{System.IO.Path.PathSeparator}{System.IO.Path.PathSeparator}{Second}";

    public static void AddFile(string directory, string name, bool executable)
    {
        var file = System.IO.Path.Combine(directory, name);
        if (executable)
        {
            _ = ExecutableScript.Write(file, string.Empty);
            return;
        }

        File.WriteAllText(file, "#!/bin/sh\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public void Dispose()
    {
        Directory.Delete(First, recursive: true);
        Directory.Delete(Second, recursive: true);
    }
}
