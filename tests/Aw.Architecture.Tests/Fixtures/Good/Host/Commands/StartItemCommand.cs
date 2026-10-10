using Aw.Architecture.Tests.Fixtures.Good.Features.StartItem;

namespace Aw.Architecture.Tests.Fixtures.Good.Host.Commands;

internal static class StartItemCommand
{
    public static int Run(IServiceProvider services, string name) =>
        services.GetService(typeof(StartItemService)) is StartItemService service && service.Start(name) is not null ? 0 : 1;
}
