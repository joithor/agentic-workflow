using System.Diagnostics;

namespace Aw.Architecture.Tests.TmuxFixtures;

/// <summary>Runs tmux with the five read verbs and -S.</summary>
internal static class GoodCli
{
    internal static void Run(string socket, string verb) => _ = Process.Start(new ProcessStartInfo("tmux", ["-S", socket, verb]));   // tmux-isolation: ok, an ARCH-SESS-001 fixture read as IL, never run

    internal static string[] Verbs() => ["list-sessions", "list-panes", "list-clients", "display-message", "capture-pane"];
}
