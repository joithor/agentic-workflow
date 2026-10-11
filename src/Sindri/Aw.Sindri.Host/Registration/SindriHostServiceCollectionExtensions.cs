using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Server;

namespace Aw.Sindri.Host.Registration;

/// <summary>Registers the Host's own types: the shutdown port, the exit-code sink, the serve port, and serve's background services.</summary>
internal static class SindriHostServiceCollectionExtensions
{
    /// <summary>Adds the process shutdown (also as IShutdown) and the exit code.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddSindriHost(this IServiceCollection services) => services
        .AddSingleton<ProcessShutdown>()
        .AddSingleton<IShutdown>(static sp => sp.GetRequiredService<ProcessShutdown>())
        .AddSingleton<IExitCode, ProcessExitCode>();

    /// <summary>Adds what only 'sindri serve' runs: the scheduler, the lease renewer, and the port it chooses before it binds.</summary>
    /// <param name="services">The service collection (already composed).</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddSindriServe(this IServiceCollection services) => services
        .AddSingleton<ServePort>()
        .AddHostedService<SchedulerHost>()
        .AddHostedService<LeaseRenewer>();
}
