using Aw.Kernel.Ids;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Auth;
using Aw.Sindri.Infrastructure.Challenges;
using Aw.Sindri.Infrastructure.Control;
using Aw.Sindri.Infrastructure.Lease;
using Aw.Sindri.Infrastructure.Notifications;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Infrastructure.Profile;
using Aw.Sindri.Infrastructure.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Infrastructure.Registration;

/// <summary>Registers Aw.Sindri.Infrastructure's adapters and settings. A second technology for a port gets its own AddSindri&lt;Tech&gt;Infrastructure method.</summary>
public static class SindriInfrastructureServiceCollectionExtensions
{
    /// <summary>Adds the Sindri adapters (SQLite ledger and lease, YAML profile and recipes, files, the process probe, process ancestry, git repo resolution) and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <remarks>The profile and approval adapters take the package's AwJson options, which Compose registers.</remarks>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddSindriInfrastructure(this IServiceCollection services, Func<string, string?> environment)
        => services
            .AddSingleton(SettingsFromEnvironment.Read(environment))
            .AddSingleton<IIdGenerator, GuidV7Ids>()
            .AddSingleton<IProcessProbe, PosixProcessProbe>()
            .AddSingleton<LedgerDatabase>()
            .AddSingleton<ILedgerStore, SqliteLedger>()
            .AddSingleton<ILedgerReader, SqliteLedgerReader>()
            .AddSingleton<ILease, SqliteLease>()
            .AddSingleton<IProfileSource, YamlProfileSource>()
            .AddSingleton<IApprovals, FileApprovals>()
            .AddSingleton<IApprovedProfile>(static sp => sp.GetRequiredService<IApprovals>())
            .AddSingleton<IEndpointFile, FileEndpoint>()
            .AddSingleton<IControlPlaneProbe, HttpControlPlaneProbe>()
            .AddSingleton<IControlPlaneLauncher, ServeLauncher>()
            .AddSingleton<IHumanTokenStore, FileHumanToken>()
            .AddSingleton(static sp => new ProverLabel(sp.GetRequiredService<SindriSettings>().ProverLabelAllowed))
            .AddSingleton<IProcessAncestry, PosixProcessAncestry>()
            .AddSingleton<IRepoLocator, GitRepoLocator>()
            .AddSingleton<ICommandOrigins, LedgerCommandOrigins>()   // engine P4 unit (d): the bridge's causation route
            .AddSingleton<IRecipeReader, YamlRecipeReader>()
            .AddSingleton<IRecipeFiles, FileRecipeFiles>()
            .AddSingleton<IRecipeWake, NoOpRecipeWake>()
            .AddSingleton<IRecipeStore, SqliteRecipeStore>()
            .AddSingleton<IRecipeApprovals, SqliteRecipeApprovals>()
            .AddSingleton<IChallenges, ProcessChallenges>()
            .AddSingleton<IHarnessVersions, CommandHarnessVersions>()
            .AddSingleton<IOsNotifier, OsascriptNotifier>()          // engine P6 unit (c): the only OS notifier (README X5, X50)
            .AddSingleton<IDeskNotices, FileDeskNotices>();          // engine P6 unit (c): notifications.jsonl
}
