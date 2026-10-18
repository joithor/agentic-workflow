using System.Diagnostics;

namespace Aw.Architecture.Tests.TmuxFixtures;

/// <summary>Runs tmux with the four read verbs and -S.</summary>
internal static class GoodCli
{
    internal static void Run(string socket, string verb) => _ = Process.Start(new ProcessStartInfo("tmux", ["-S", socket, verb]));

    internal static string[] Verbs() => ["list-sessions", "list-panes", "list-clients", "display-message"];
}
