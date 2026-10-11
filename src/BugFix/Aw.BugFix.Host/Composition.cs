using Aw.BugFix.Core;
using Aw.BugFix.Features;
using Aw.BugFix.Infrastructure;
using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.BugFix.Host;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddSingleton(AwJson.CreateOptions(BugFixJsonContext.Default))   // the one AwJson profile: state and run files, the ticket, CLI output
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("bugfix-state", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddBugFixCore()
            .AddBugFixFeatures()
            .AddBugFixInfrastructure(environment);
}
