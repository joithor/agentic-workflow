using System.Text.Json;
using Aw.Kernel.Telemetry;
using Aw.Meta.Host;
using Aw.Meta.Host.Commands;
using Microsoft.Extensions.DependencyInjection;

var (arguments, environment, problem) = TeamOption.Apply(args, Environment.GetEnvironmentVariable);
if (problem.Length > 0)
{
    return CommandOutput.Usage(ConsoleIo.Standard(), problem, "[--team <name>] <command> [arguments]");
}

var services = Composition.Compose(new ServiceCollection(), environment, TimeProvider.System);
var provider = services.BuildServiceProvider();   // validation runs in HostTests, not at CLI startup
await using (provider.ConfigureAwait(false))
{
    _ = provider.GetRequiredService<TelemetryRuntime>();   // resolved so the container disposes it on exit, which flushes
    return await CommandTable.Run(arguments, provider, provider.GetRequiredService<JsonSerializerOptions>(), ConsoleIo.Standard() with { Env = environment }).ConfigureAwait(false);
}
