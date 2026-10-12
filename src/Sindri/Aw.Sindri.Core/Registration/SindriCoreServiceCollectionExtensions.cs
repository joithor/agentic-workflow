using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Core.Registration;

/// <summary>Registers Aw.Sindri.Core's types.</summary>
public static class SindriCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Sindri domain services: the control plane's event scope and factory, the caller of the code running now,
    /// the lease and human token this process holds, and the projector's progress (status and rebuild control).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriCore(this IServiceCollection services)
        => services
            .AddSingleton(EventScope.ControlPlane)
            .AddSingleton<EventFactory>()
            .AddSingleton<ICallerContextAccessor, AsyncLocalCallerContextAccessor>()
            .AddSingleton<HeldLease>()
            .AddSingleton<HeldHumanToken>()
            .AddSingleton<BlockCatalog>()
            .AddSingleton<ProjectorProgress>()
            .AddSingleton<IProjectorStatus>(static sp => sp.GetRequiredService<ProjectorProgress>())
            .AddSingleton<IProjectorControl>(static sp => sp.GetRequiredService<ProjectorProgress>());
}
