using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Infrastructure;

namespace Aw.Sindri.Host;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("sindri", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddSindriCore()
            .AddSindriFeatures()
            .AddSindriInfrastructure(environment)
            .AddSingleton<ProcessShutdown>()
            .AddSingleton<IShutdown>(static sp => sp.GetRequiredService<ProcessShutdown>())
            .AddScoped<IScheduledService, HeartbeatService>();
}
