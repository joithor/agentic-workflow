using Aw.Kernel.Telemetry;
using Aw.Meta.Core;
using Aw.Meta.Features;
using Aw.Meta.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Meta.Host;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("aw-meta", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddMetaCore()
            .AddMetaFeatures()
            .AddMetaInfrastructure(environment);
}
