using Aw.Architecture.Tests.Fixtures.PortSmall.App.Infrastructure;

namespace Aw.Architecture.Tests.Fixtures.PortSmall.App.Commands;

internal static class DrainCommand
{
    public static int Run() => JobQueue.Depth;
}
