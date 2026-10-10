using Aw.Architecture.Tests.Fixtures.Good.Core;

namespace Aw.Architecture.Tests.Fixtures.Good.Infrastructure;

public static class GoodInfrastructureServiceCollectionExtensions
{
    public static Func<IServiceProvider, IItemStore?> Factory { get; } =
        static sp => sp.GetService(typeof(IItemStore)) as IItemStore;
}
