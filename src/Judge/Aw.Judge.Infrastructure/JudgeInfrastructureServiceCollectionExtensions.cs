using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Infrastructure;

/// <summary>Registers Aw.Judge.Infrastructure's adapters and settings. A second technology for a port gets its own AddJudge&lt;Tech&gt;Infrastructure method.</summary>
public static class JudgeInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Judge adapters and their settings: the SQLite stores over $AW_STATE_DIR/judge/decisions.sqlite.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddJudgeInfrastructure(this IServiceCollection services, Func<string, string?> environment)
    {
        var settings = SettingsFromEnvironment.Read(environment);
        return services
            .AddSingleton(settings)
            .AddSingleton(sp => new JudgeDatabase(JudgeDatabase.OptionsFor(settings.StateDirectory), sp.GetRequiredService<TimeProvider>()))
            .AddScoped<IDecisionLog, SqliteDecisionLog>();
    }
}
