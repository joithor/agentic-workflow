using System.Diagnostics;

namespace Aw.Architecture.Tests.TmuxFixtures;

/// <summary>Starts tmux without going through the cli type.</summary>
internal static class StrayRunner
{
    internal static void Run() => _ = Process.Start(new ProcessStartInfo("tmux"));
}
