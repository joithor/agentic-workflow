using System.Diagnostics;

namespace Aw.Architecture.Tests.ProcessFixtures;

/// <summary>The one type allowed to construct a ProcessStartInfo (an ARCH-PROC-001 fixture read as IL, never run).</summary>
internal static class FactoryFixture
{
    internal static ProcessStartInfo Start(string file) => new(file);
}
