using System.Diagnostics;

namespace Aw.Architecture.Tests.TmuxFixtures;

/// <summary>Takes the attach line and runs it.</summary>
internal static class RunsAttachLine
{
    internal static void Run(string socket)
    {
        var line = GoodAttachLine.Build(socket, "s");
        _ = Process.Start(new ProcessStartInfo(line[0]));
    }
}
