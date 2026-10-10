using Aw.Architecture.Tests.Fixtures.Good.Core;
using Aw.Architecture.Tests.Fixtures.Good.Infrastructure;

namespace Aw.Architecture.Tests.Fixtures.Good.Host;

internal static class Composition
{
    public static Func<IServiceProvider, IItemStore?> Store => GoodInfrastructureServiceCollectionExtensions.Factory;
}
