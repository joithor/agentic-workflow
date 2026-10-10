using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Kernel.Telemetry;

/// <summary>Registers Aw.Kernel.Telemetry (spec §4b.4: one explicit method per project; only the Host calls it).</summary>
public static class KernelTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="TelemetryRuntime"/> (a singleton, started on first resolve), its logger factory as
    /// ILoggerFactory, and ILogger of T.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="options">The telemetry options.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddKernelTelemetry(this IServiceCollection services, TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _ = services.AddSingleton(_ => TelemetryRuntime.Start(options));
        _ = services.AddSingleton(static sp => sp.GetRequiredService<TelemetryRuntime>().LoggerFactory);
        _ = services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        return services;
    }
}
