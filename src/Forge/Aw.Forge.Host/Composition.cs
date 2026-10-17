using Aw.Forge.Core;
using Aw.Forge.Features;
using Aw.Forge.Infrastructure;
using Aw.Kernel.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Host;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("aw-forge-fake", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddForgeCore()
            .AddForgeFeatures()
            .AddForgeInfrastructure(environment);
}
