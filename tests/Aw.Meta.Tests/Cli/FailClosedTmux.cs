using Aw.Kernel.Testing;
namespace Aw.Meta.Tests.Cli;

/// <summary>A stand-in for the tmux program that records its arguments and fails: what AW_TMUX points at in every aw-meta test, so none of them can reach the real tmux server.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal static class FailClosedTmux
{
    internal const string Marker = "# aw-meta test stub: fail-closed tmux";

    /// <summary>Writes the stub into <paramref name="directory"/> and returns its path.</summary>
    internal static string Write(string directory)
    {
        var path = Path.Combine(directory, "tmux-stub");
        return ExecutableScript.Write(path, $"{Marker}\necho \"$@\" >> '{Path.Combine(directory, "tmux-stub.log")}'\nexit 1\n");
    }
}
