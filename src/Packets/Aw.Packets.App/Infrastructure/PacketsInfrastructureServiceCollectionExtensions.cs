using System.Text.Json;
using Aw.Packets.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Infrastructure;

/// <summary>Registers Aw.Packets.App.Infrastructure's adapters and settings. A second technology for a port gets its own AddPackets&lt;Tech&gt;Infrastructure method.</summary>
internal static class PacketsInfrastructureServiceCollectionExtensions
{
    /// <summary>How long one git run may take.</summary>
    internal static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Adds the Packets adapters (disk, git, the freeze log under packets/) and their settings.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">Reads an environment variable (Environment.GetEnvironmentVariable in Program.cs, a lambda in tests).</param>
    /// <param name="json">The packet file options (AwJson over the package's JSON context, which Composition owns).</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPacketsInfrastructure(this IServiceCollection services, Func<string, string?> environment, JsonSerializerOptions json)
        => services
            .AddSingleton(SettingsFromEnvironment.Read(environment))
            .AddSingleton<IPacketSource>(_ => new FileSystemPacketSource(json))
            .AddSingleton<IRepoSnapshot>(static _ => new GitRepoSnapshot(GitTimeout))
            .AddSingleton<IFreezeLog>(sp => new FileFreezeLog(sp.GetRequiredService<PacketsSettings>(), Indented(json)));

    private static JsonSerializerOptions Indented(JsonSerializerOptions json) => new(json) { WriteIndented = true };
}
