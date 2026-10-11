using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Server;

namespace Aw.Sindri.Host.Registration;

/// <summary>Registers the Host's own types: the shutdown port and the exit-code sink.</summary>
internal static class SindriHostServiceCollectionExtensions
{
    /// <summary>Adds the process shutdown (also as IShutdown) and the exit code.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddSindriHost(this IServiceCollection services) => services
        .AddSingleton<ProcessShutdown>()
        .AddSingleton<IShutdown>(static sp => sp.GetRequiredService<ProcessShutdown>())
        .AddSingleton<IExitCode, ProcessExitCode>();
}
