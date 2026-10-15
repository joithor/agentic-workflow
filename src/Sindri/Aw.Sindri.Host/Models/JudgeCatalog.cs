using System.Text.Json;
using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// Judge's provider catalog, built on first use in a container of its own: Judge's infrastructure registers its own stores, settings and
/// id source, none of which may join Sindri's container, so Sindri takes only the catalog out of it (context spec §4).
/// </summary>
/// <param name="environment">Reads an environment variable (AW_PROVIDER, PATH).</param>
/// <param name="clock">The clock.</param>
/// <param name="json">The JSON options Judge's container is given (its config file isn't read here).</param>
internal sealed class JudgeCatalog(Func<string, string?> environment, TimeProvider clock, JsonSerializerOptions json)
{
    private readonly Lazy<ServiceProvider> _provider = new(() => new ServiceCollection()
        .AddSingleton(clock)
        .AddSingleton(json)
        .AddJudgeInfrastructure(environment)
        .BuildServiceProvider());

    /// <summary>The catalog, building Judge's container on the first call.</summary>
    /// <returns>The provider catalog.</returns>
    internal IProviderCatalog Get() => _provider.Value.GetRequiredService<IProviderCatalog>();
}
