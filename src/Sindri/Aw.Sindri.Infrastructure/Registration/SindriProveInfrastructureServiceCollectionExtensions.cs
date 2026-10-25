using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Infrastructure.Prove;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Registration;

/// <summary>The prove gate and the files behind the test-only blocks (plan decision 9, engine Q2).</summary>
public static class SindriProveInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// True when the test-only blocks may register: AW_SINDRI_PROVE_BLOCKS is 1 and the home is a prover home (inside the temp
    /// directory or a proof folder), so the owner's real home never qualifies.
    /// </summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>Whether the gate is open.</returns>
    public static bool ProveBlocksAllowed(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return string.Equals(environment("AW_SINDRI_PROVE_BLOCKS"), "1", StringComparison.Ordinal) && SettingsFromEnvironment.Read(environment).ProverLabelAllowed;
    }

    /// <summary>Adds the echo input's files, 'prove/echo.txt' and 'prove/echo.log' under the home.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriProveInfrastructure(this IServiceCollection services, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return services.AddSingleton<IProveEcho>(new ProveEchoFiles(Path.Combine(SettingsFromEnvironment.Read(environment).Home, "prove")));
    }
}
