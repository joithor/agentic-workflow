using Aw.Architecture.Tests.Fixtures.PortSmall.App.Infrastructure;

namespace Aw.Architecture.Tests.Fixtures.PortSmall.App;

internal static class Composition
{
    public static int Depth => JobQueue.Depth;
}
