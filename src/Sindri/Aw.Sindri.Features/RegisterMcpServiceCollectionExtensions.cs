using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.RegisterMcp;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the RegisterMcp job (setup-once spec §5.4).</summary>
internal static class RegisterMcpServiceCollectionExtensions
{
    /// <summary>Adds the RegisterMcp job keyed by 'mcp-' and the hash of the repo's name, and its admission.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRegisterMcpJob(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var resolved = new ResolvedMcpRepos();
        return services
            .AddSingleton(resolved)
            .AddScoped<IJobAdmission<RegisterMcpJobRequest>, RegisterMcpAdmission>()
            .AddJob<RegisterMcpJobRequest, RegisterMcpResponse, RegisterMcpJobService>(resolved.KeyOf, static r => "registered in " + r.Repo.Value);
    }
}
