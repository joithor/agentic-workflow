using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aw.Sindri.Tests.Server;

/// <summary>
/// Swaps the real dev server for <see cref="FakeTemporalServer"/> in a host test that starts the control plane: those tests
/// are about the API and the lease, and the real server has its own integration tests (Workflows/).
/// </summary>
internal static class NoTemporal
{
    /// <summary>Registers a new <see cref="FakeTemporalServer"/> as the <see cref="ITemporalServer"/>, and a worker that polls nothing until it is stopped.</summary>
    /// <param name="services">The host's services.</param>
    public static void Use(IServiceCollection services) => Use(services, new FakeTemporalServer());

    /// <summary>Registers <paramref name="fake"/> as the <see cref="ITemporalServer"/>, and a worker that polls nothing until it is stopped.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="fake">The fake to use.</param>
    public static void Use(IServiceCollection services, FakeTemporalServer fake) => Use(services, fake, new ScriptedWorker());

    /// <summary>Registers <paramref name="fake"/> as the <see cref="ITemporalServer"/> and <paramref name="worker"/> as the worker.</summary>
    /// <param name="services">The host's services.</param>
    /// <param name="fake">The fake server.</param>
    /// <param name="worker">The fake worker.</param>
    public static void Use(IServiceCollection services, FakeTemporalServer fake, ITemporalWorker worker)
    {
        _ = services.Replace(ServiceDescriptor.Singleton<ITemporalServer>(fake));
        _ = services.Replace(ServiceDescriptor.Singleton(worker));
    }

    /// <summary>A new fake, then <paramref name="more"/>.</summary>
    /// <param name="more">The test's own configuration.</param>
    /// <returns>The combined configuration.</returns>
    public static Action<IServiceCollection> And(Action<IServiceCollection>? more) => services =>
    {
        Use(services);
        more?.Invoke(services);
    };
}
