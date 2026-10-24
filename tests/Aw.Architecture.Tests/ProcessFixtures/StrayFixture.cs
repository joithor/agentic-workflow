using System.Diagnostics;

namespace Aw.Architecture.Tests.ProcessFixtures;

/// <summary>Constructs a ProcessStartInfo outside the factory, inside a lambda (an ARCH-PROC-001 fixture read as IL, never run).</summary>
internal static class StrayFixture
{
    internal static Func<ProcessStartInfo> Make(string file) => () => new ProcessStartInfo(file);
}
