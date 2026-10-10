using Aw.Readiness.App.Features;
using Aw.Readiness.App.Infrastructure;
using Aw.Readiness.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Readiness.App;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddReadinessCore()
            .AddReadinessFeatures()
            .AddReadinessInfrastructure(environment);
}
