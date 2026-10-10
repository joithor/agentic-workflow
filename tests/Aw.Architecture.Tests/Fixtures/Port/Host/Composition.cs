using Aw.Architecture.Tests.Fixtures.Port.Infrastructure;

namespace Aw.Architecture.Tests.Fixtures.Port.Host;

internal static class Composition
{
    public static string Adapter => Fetcher.Name;
}
