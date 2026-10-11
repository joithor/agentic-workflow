using System.Text.Json;
using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Config;
using Aw.Judge.Infrastructure.Ids;
using Aw.Judge.Infrastructure.Processes;
using Aw.Judge.Infrastructure.Redaction;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Kernel.Ids;
using Aw.Redaction;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Infrastructure;

/// <summary>Registers Aw.Judge.Infrastructure's adapters and settings. A second technology for a port gets its own AddJudge&lt;Tech&gt;Infrastructure method.</summary>
public static class JudgeInfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Judge adapters and their settings: the SQLite stores over $AW_STATE_DIR/judge/decisions.sqlite, the config
    /// file $AW_STATE_DIR/judge/config.json (which needs the host's JsonSerializerOptions registered), the input redactor
    /// over Aw.Redaction, the PATH executable locator and random decision ids.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddJudgeInfrastructure(this IServiceCollection services, Func<string, string?> environment)
    {
        var settings = SettingsFromEnvironment.Read(environment);
        return services
            .AddSingleton(settings)
            .AddSingleton(sp => new JudgeDatabase(JudgeDatabase.OptionsFor(settings.StateDirectory), sp.GetRequiredService<TimeProvider>()))
            .AddScoped<IDecisionLog, SqliteDecisionLog>()
            .AddScoped<IBriefStore, SqliteBriefStore>()
            .AddScoped<IJudgeConfigStore>(sp => new FileJudgeConfigStore(settings.StateDirectory, sp.GetRequiredService<JsonSerializerOptions>()))
            .AddSingleton<IInputRedactor>(new AwInputRedactor(Redactor.Default))
            .AddSingleton<IExecutableLocator>(new PathExecutableLocator(environment))
            .AddSingleton<IProcessRunner>(new SystemProcessRunner(environment))
            .AddSingleton<IIdGenerator, RandomIdGenerator>();
    }
}
