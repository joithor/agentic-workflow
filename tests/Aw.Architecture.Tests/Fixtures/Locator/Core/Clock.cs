namespace Aw.Architecture.Tests.Fixtures.Locator.Core;

internal sealed class Clock(IServiceProvider services)
{
    public object? Now => services.GetService(typeof(TimeProvider));
}
