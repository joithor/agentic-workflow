using Aw.Packets.Core.Kinds.Line;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.Core;

/// <summary>Registers Aw.Packets.Core's types.</summary>
public static class PacketsCoreServiceCollectionExtensions
{
    /// <summary>Adds the Packets domain services.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddPacketsCore(this IServiceCollection services) =>
        services.AddScoped<PacketInspector>().AddScoped<LinePacketInspector>();
}
