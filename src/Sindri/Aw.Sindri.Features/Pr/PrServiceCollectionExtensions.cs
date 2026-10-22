using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Pr;

/// <summary>Registers what the PR babysitter's agent Steps are told (PR babysitter spec Appendix A).</summary>
internal static class PrServiceCollectionExtensions
{
    /// <summary>Adds the instructions of the babysitter's agent Steps.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddPrSlices(this IServiceCollection services) => services
        .AddSingleton<IAgentStepInstructions, CiFixInstructions>();
}
