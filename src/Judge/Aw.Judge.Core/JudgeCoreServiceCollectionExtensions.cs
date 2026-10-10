using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Core;

/// <summary>Registers Aw.Judge.Core's types.</summary>
public static class JudgeCoreServiceCollectionExtensions
{
    /// <summary>Adds the Judge domain services (none yet).</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddJudgeCore(this IServiceCollection services) => services;
}
