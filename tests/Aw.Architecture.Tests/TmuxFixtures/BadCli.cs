using System.Diagnostics;

namespace Aw.Architecture.Tests.TmuxFixtures;

/// <summary>Names a fifth verb, three of the four, and no socket flag.</summary>
internal static class BadCli
{
    internal static void Run() => _ = Process.Start(new ProcessStartInfo("tmux", ["kill-server"]));   // tmux-isolation: ok, an ARCH-SESS-001 fixture read as IL, never run

    internal static string[] Verbs() => ["list-sessions", "list-panes"];
}
