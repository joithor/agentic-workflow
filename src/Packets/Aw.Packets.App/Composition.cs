using Aw.Kernel.Json;
using Aw.Packets.App.Features;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
    {
        return services
            .AddSingleton(clock)
            .AddSingleton(AwJson.CreateOptions(PacketsJsonContext.Default))   // the one Kernel JSON profile: output and state files
            .AddPacketsCore()
            .AddPacketsFeatures()
            .AddPacketsInfrastructure(environment);
    }
}
