using Aw.Packets.App.Features;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddPacketsCore()
            .AddPacketsFeatures()
            .AddPacketsInfrastructure(environment);
}
