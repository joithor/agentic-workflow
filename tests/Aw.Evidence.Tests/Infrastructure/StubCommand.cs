namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>An executable bash script standing in for a real program (docker), written into a temp directory.</summary>
internal static class StubCommand
{
    internal static string Write(TempDirectory root, string name, string body)
    {
        var path = root.Combine(name);
        File.WriteAllText(path, "#!/usr/bin/env bash\n" + body + "\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return path;
    }
}
