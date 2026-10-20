using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>An executable bash script standing in for a real program (docker), written into a temp directory.</summary>
internal static class StubCommand
{
    internal static string Write(TempDirectory root, string name, string body)
    {
        var path = root.Combine(name);
        return ExecutableScript.Write(path, body + "\n", "/usr/bin/env bash");
    }
}
