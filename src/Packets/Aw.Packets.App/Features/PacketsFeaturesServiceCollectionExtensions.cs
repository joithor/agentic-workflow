using Aw.Kernel.Services;
using Aw.Packets.App.Features.FreezePacket;
using Aw.Packets.App.Features.ValidatePacket;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Features;

/// <summary>Registers Aw.Packets.App.Features's slices: each service, its validator and its decorators (outermost first).</summary>
internal static class PacketsFeaturesServiceCollectionExtensions
{
    /// <summary>Adds every Packets use case as an IService.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPacketsFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<ValidatePacketService>()
            .AddScoped<IValidator<ValidatePacketRequest>, ValidatePacketValidator>()
            .AddScoped<IService<ValidatePacketRequest, ValidatePacketResponse>>(static sp =>
                new ValidatingService<ValidatePacketRequest, ValidatePacketResponse>(
                    sp.GetRequiredService<ValidatePacketService>(),
                    sp.GetRequiredService<IValidator<ValidatePacketRequest>>()));
        _ = services
            .AddScoped<FreezePacketService>()
            .AddScoped<IValidator<FreezePacketRequest>, FreezePacketValidator>()
            .AddScoped<IService<FreezePacketRequest, FreezePacketResponse>>(static sp =>
                new ValidatingService<FreezePacketRequest, FreezePacketResponse>(
                    sp.GetRequiredService<FreezePacketService>(),
                    sp.GetRequiredService<IValidator<FreezePacketRequest>>()));
        return services;
    }
}
