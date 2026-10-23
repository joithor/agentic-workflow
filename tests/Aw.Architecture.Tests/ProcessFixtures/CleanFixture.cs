using System.Diagnostics;

namespace Aw.Architecture.Tests.ProcessFixtures;

/// <summary>Takes a ProcessStartInfo from the factory and only sets properties on it: no construction (an ARCH-PROC-001 fixture).</summary>
internal static class CleanFixture
{
    internal static void Run(string file)
    {
        var start = FactoryFixture.Start(file);
        start.UseShellExecute = false;
        _ = start.Environment.Remove("TMUX");
    }
}
