namespace Aw.Architecture.Tests.Fixtures.Good.Core;

// The scaffold's Add<Name>Core: registrations only (the fixture has no DI package, so it models the shape).
public static class GoodCoreServiceCollectionExtensions
{
    public static IReadOnlyList<Type> AddGoodCore() => [typeof(IItemStore)];
}
