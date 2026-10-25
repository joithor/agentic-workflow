using Aw.Sindri.Core.Workflow.Blocks;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Review;

/// <summary>Registers the block runtime of the review result's Step (line spec §7.4); its descriptor is registered with the blocks.</summary>
internal static class ReviewServiceCollectionExtensions
{
    /// <summary>Adds the review record Step's runtime by block id.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddReviewSlices(this IServiceCollection services) => services
        .AddBlockRuntime<ReviewRecordStep>();
}
