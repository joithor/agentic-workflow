namespace Aw.Architecture.Tests.Fixtures.Locator.Features.Run;

internal sealed class RunService(IServiceProvider services)
{
    public object? Clock => services.GetService(typeof(TimeProvider));
}
