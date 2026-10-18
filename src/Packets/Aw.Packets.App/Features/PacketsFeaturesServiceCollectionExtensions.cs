using Aw.Kernel.Services;
using Aw.Packets.App.Features.FreezeLinePacket;
using Aw.Packets.App.Features.FreezePacket;
using Aw.Packets.App.Features.FreezeScopingPacket;
using Aw.Packets.App.Features.HashPacket;
using Aw.Packets.App.Features.ValidateLinePacket;
using Aw.Packets.App.Features.ValidatePacket;
using Aw.Packets.App.Features.ValidateScopingPacket;
using Aw.Packets.App.Features.VerifyPacket;
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
        _ = services
            .AddScoped<ValidateScopingPacketService>()
            .AddScoped<IValidator<ValidateScopingPacketRequest>, ValidateScopingPacketValidator>()
            .AddScoped<IService<ValidateScopingPacketRequest, ValidateScopingPacketResponse>>(static sp =>
                new ValidatingService<ValidateScopingPacketRequest, ValidateScopingPacketResponse>(
                    sp.GetRequiredService<ValidateScopingPacketService>(),
                    sp.GetRequiredService<IValidator<ValidateScopingPacketRequest>>()));
        _ = services
            .AddScoped<HashPacketService>()
            .AddScoped<IValidator<HashPacketRequest>, HashPacketValidator>()
            .AddScoped<IService<HashPacketRequest, HashPacketResponse>>(static sp =>
                new ValidatingService<HashPacketRequest, HashPacketResponse>(
                    sp.GetRequiredService<HashPacketService>(),
                    sp.GetRequiredService<IValidator<HashPacketRequest>>()));
        _ = services
            .AddScoped<FreezeScopingPacketService>()
            .AddScoped<IValidator<FreezeScopingPacketRequest>, FreezeScopingPacketValidator>()
            .AddScoped<IService<FreezeScopingPacketRequest, FreezeScopingPacketResponse>>(static sp =>
                new ValidatingService<FreezeScopingPacketRequest, FreezeScopingPacketResponse>(
                    sp.GetRequiredService<FreezeScopingPacketService>(),
                    sp.GetRequiredService<IValidator<FreezeScopingPacketRequest>>()));
        return services.AddLinePacketFeatures();
    }

    private static IServiceCollection AddLinePacketFeatures(this IServiceCollection services)
    {
        _ = services
            .AddScoped<ValidateLinePacketService>()
            .AddScoped<IValidator<ValidateLinePacketRequest>, ValidateLinePacketValidator>()
            .AddScoped<IService<ValidateLinePacketRequest, ValidateLinePacketResponse>>(static sp =>
                new ValidatingService<ValidateLinePacketRequest, ValidateLinePacketResponse>(
                    sp.GetRequiredService<ValidateLinePacketService>(),
                    sp.GetRequiredService<IValidator<ValidateLinePacketRequest>>()));
        _ = services
            .AddScoped<FreezeLinePacketService>()
            .AddScoped<IValidator<FreezeLinePacketRequest>, FreezeLinePacketValidator>()
            .AddScoped<IService<FreezeLinePacketRequest, FreezeLinePacketResponse>>(static sp =>
                new ValidatingService<FreezeLinePacketRequest, FreezeLinePacketResponse>(
                    sp.GetRequiredService<FreezeLinePacketService>(),
                    sp.GetRequiredService<IValidator<FreezeLinePacketRequest>>()));
        return services.AddVerifyFeature();
    }

    private static IServiceCollection AddVerifyFeature(this IServiceCollection services)
    {
        _ = services
            .AddScoped<VerifyPacketService>()
            .AddScoped<IValidator<VerifyPacketRequest>, VerifyPacketValidator>()
            .AddScoped<IService<VerifyPacketRequest, VerifyPacketResponse>>(static sp =>
                new ValidatingService<VerifyPacketRequest, VerifyPacketResponse>(
                    sp.GetRequiredService<VerifyPacketService>(),
                    sp.GetRequiredService<IValidator<VerifyPacketRequest>>()));
        return services;
    }
}
