using Aw.Judge.Core;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features;
using Aw.Judge.Host.Commands;
using Aw.Judge.Infrastructure;
using Aw.Kernel.Json;
using Aw.Kernel.Telemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host;

/// <summary>The one composition root, shared by Program.cs and the DI validation test.</summary>
internal static class Composition
{
    internal static IServiceCollection Compose(IServiceCollection services, Func<string, string?> environment, TimeProvider clock)
        => services
            .AddSingleton(clock)
            .AddSingleton(AwJson.CreateOptions(JudgeJsonContext.Default))   // the Kernel JSON profile: the config file and Core's stored-input JSON
            .AddKernelTelemetry(TelemetryOptions.FromEnvironment("judge", environment, clock))   // JSON lines on stderr (AW_LOG_LEVEL); exports off
            .AddSingleton(CallingSession.From(environment("AW_SESSION_ID")))
            .AddSingleton(ModelProviders())
            .AddJudgeCore()
            .AddJudgeFeatures()
            .AddJudgeInfrastructure(environment);

    // The one place the questions' providers and chain are built. Offline for now: rules alone, so any input no pre-rule
    // settles escalates 'no-provider-decided'. The agent-CLI and Jev adapters, the chain from config.json and AW_PROVIDER,
    // and the --providers allowlist arrive here (and in AskQuestionRequest.AllowedProviders) with the provider work.
    private static ProviderSet ModelProviders() => new([new RulesProvider()], ChainSpec.Default);
}
