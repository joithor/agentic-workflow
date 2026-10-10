using Aw.Kernel.Services;
using Aw.Readiness.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Readiness.Tests;

public sealed class HostTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_registered_service_resolves_in_a_scope()
    {
        var stateRoot = Path.Combine(Path.GetTempPath(), "aw-readiness-di-test");
        var services = Composition.Compose(
            new ServiceCollection(), name => name == "AW_STATE_DIR" ? stateRoot : null, new FakeTimeProvider());
        var serviceTypes = services
            .Select(d => d.ServiceType)
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IService<,>))
            .ToList();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotEmpty(serviceTypes);
        Assert.All(serviceTypes, t => Assert.NotNull(scope.ServiceProvider.GetRequiredService(t)));
    }
}
