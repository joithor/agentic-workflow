using Aw.Kernel.Services;
using Aw.Kernel.Telemetry;
using Aw.Packets.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Packets.Tests;

public sealed class HostTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-packets-di-test");

    [Fact, Trait("Lane", "unit")]
    public void Every_registered_service_resolves_in_a_scope()
    {
        var services = Compose();
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

    [Fact, Trait("Lane", "unit")]
    public void Telemetry_is_composed_and_exports_nothing()
    {
        using var provider = Compose().BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var runtime = provider.GetRequiredService<TelemetryRuntime>();

        Assert.False(runtime.IsExporting);
        Assert.Same(runtime.LoggerFactory, provider.GetRequiredService<ILoggerFactory>());
        Assert.NotNull(provider.GetRequiredService<ILogger<HostTests>>());
    }

    private static IServiceCollection Compose()
        => Composition.Compose(
            new ServiceCollection(), name => name == "AW_STATE_DIR" ? _stateRoot : null, new FakeTimeProvider());
}
