using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry;

/// <summary>Registers Aw.Kernel.Telemetry (spec §4b.4: one explicit method per project; only the Host calls it).</summary>
public static class KernelTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Starts the <see cref="TelemetryRuntime"/> now (so spans are recorded and a <see cref="TelemetryOptions.Problem"/>
    /// is written while Compose runs, before anything resolves a logger) and registers it as a singleton the container
    /// disposes, with its logger factory as ILoggerFactory and ILogger of T.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="options">The telemetry options.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddKernelTelemetry(this IServiceCollection services, TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var runtime = TelemetryRuntime.Start(options);
        _ = services.AddSingleton(_ => runtime);
        _ = services.AddSingleton(static sp => sp.GetRequiredService<TelemetryRuntime>().LoggerFactory);
        _ = services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        return services;
    }
}
