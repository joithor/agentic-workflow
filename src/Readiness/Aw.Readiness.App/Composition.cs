using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Aw.Readiness.App.Features;
using Aw.Readiness.App.Infrastructure;
using Aw.Readiness.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Readiness.App;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    /// <summary>The package's JSON options: the Kernel profile over its one source-generated context.</summary>
    internal static readonly JsonSerializerOptions Json = AwJson.CreateOptions(ReadinessJsonContext.Default);

    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("aw-readiness", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddSingleton(Json)
            .AddReadinessCore()
            .AddReadinessFeatures()
            .AddReadinessInfrastructure(environment);
}
