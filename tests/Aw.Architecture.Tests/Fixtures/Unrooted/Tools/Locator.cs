namespace Aw.Architecture.Tests.Fixtures.Unrooted.Tools;

internal static class Locator
{
    public static object? Clock(IServiceProvider services) => services.GetService(typeof(TimeProvider));
}
