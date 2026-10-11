using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Blocks;

/// <summary>
/// The blocks and events this build ships (spec §12): pure blocks with AddGate/AddStep/…, the P6 and P7 blocks as
/// descriptors only (AddBlockDescriptor), and the events with AddEvent. Owned by P1 unit (e); empty until it lands.
/// </summary>
internal static class BlockRegistrations
{
    /// <summary>Registers the shipped blocks and events.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRecipeBlocks(this IServiceCollection services) => services;
}
