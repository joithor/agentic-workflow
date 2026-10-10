using Aw.Architecture.Tests.Fixtures.Port.Infrastructure;

namespace Aw.Architecture.Tests.Fixtures.Port.Host.Commands;

internal static class FetchCommand
{
    public static string Run() => Fetcher.Name;
}
