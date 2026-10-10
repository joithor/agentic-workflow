namespace Aw.Architecture.Tests.Fixtures.Locator.Infrastructure;

internal sealed class Store(IServiceProvider services)
{
    public object? Clock => services.GetService(typeof(TimeProvider));
}
